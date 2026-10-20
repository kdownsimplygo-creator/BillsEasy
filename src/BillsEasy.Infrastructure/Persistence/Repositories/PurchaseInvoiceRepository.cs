using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Common;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class PurchaseInvoiceRepository : IPurchaseInvoiceRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public PurchaseInvoiceRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PurchaseInvoice?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, FinancialYearId, VoucherId, Series, InvoiceNumber,
                   InvoiceDate, SupplierId, MaterialCentre, Narration,
                   ItemSubtotal, SundrySubtotal, GrandTotal, Status,
                   TransportName, GrRrNumber, TransportDate, VehicleNumber, StationFromPlace,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM PurchaseInvoices
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var invoice = MapInvoiceFromReader(reader);
        await reader.CloseAsync();

        var lines = await LoadLinesForInvoiceAsync(connection, invoice.Id, cancellationToken);
        invoice.LoadLines(lines);

        var sundries = await LoadSundriesForInvoiceAsync(connection, invoice.Id, cancellationToken);
        invoice.LoadSundries(sundries);

        return invoice;
    }

    public async Task<PurchaseInvoice?> GetByInvoiceNumberAsync(Guid companyId, Guid financialYearId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, FinancialYearId, VoucherId, Series, InvoiceNumber,
                   InvoiceDate, SupplierId, MaterialCentre, Narration,
                   ItemSubtotal, SundrySubtotal, GrandTotal, Status,
                   TransportName, GrRrNumber, TransportDate, VehicleNumber, StationFromPlace,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM PurchaseInvoices
            WHERE CompanyId = @CompanyId AND FinancialYearId = @FinancialYearId AND InvoiceNumber = @InvoiceNumber COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.ToString());
        cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var invoice = MapInvoiceFromReader(reader);
        await reader.CloseAsync();

        var lines = await LoadLinesForInvoiceAsync(connection, invoice.Id, cancellationToken);
        invoice.LoadLines(lines);

        var sundries = await LoadSundriesForInvoiceAsync(connection, invoice.Id, cancellationToken);
        invoice.LoadSundries(sundries);

        return invoice;
    }

    public async Task<bool> ExistsByInvoiceNumberAsync(Guid companyId, Guid financialYearId, string invoiceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        var query = @"
            SELECT COUNT(1) FROM PurchaseInvoices
            WHERE CompanyId = @CompanyId AND FinancialYearId = @FinancialYearId AND InvoiceNumber = @InvoiceNumber COLLATE NOCASE
        ";
        if (excludeId.HasValue)
        {
            query += " AND Id != @ExcludeId";
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value.ToString());
        }

        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.ToString());
        cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber.Trim());

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<IReadOnlyList<PurchaseInvoice>> GetAllAsync(
        Guid companyId,
        Guid? financialYearId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var query = @"
            SELECT Id, CompanyId, FinancialYearId, VoucherId, Series, InvoiceNumber,
                   InvoiceDate, SupplierId, MaterialCentre, Narration,
                   ItemSubtotal, SundrySubtotal, GrandTotal, Status,
                   TransportName, GrRrNumber, TransportDate, VehicleNumber, StationFromPlace,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM PurchaseInvoices
            WHERE CompanyId = @CompanyId
        ";

        if (financialYearId.HasValue)
            query += " AND FinancialYearId = @FinancialYearId";

        if (fromDate.HasValue)
            query += " AND InvoiceDate >= @FromDate";

        if (toDate.HasValue)
            query += " AND InvoiceDate <= @ToDate";

        if (supplierId.HasValue)
            query += " AND SupplierId = @SupplierId";

        query += " ORDER BY InvoiceDate DESC, CreatedAt DESC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        if (financialYearId.HasValue)
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.Value.ToString());

        if (fromDate.HasValue)
            cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.ToString("yyyy-MM-dd"));

        if (toDate.HasValue)
            cmd.Parameters.AddWithValue("@ToDate", toDate.Value.ToString("yyyy-MM-dd"));

        if (supplierId.HasValue)
            cmd.Parameters.AddWithValue("@SupplierId", supplierId.Value.ToString());

        var invoices = new List<PurchaseInvoice>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            invoices.Add(MapInvoiceFromReader(reader));
        }
        await reader.CloseAsync();

        foreach (var inv in invoices)
        {
            var lines = await LoadLinesForInvoiceAsync(connection, inv.Id, cancellationToken);
            inv.LoadLines(lines);

            var sundries = await LoadSundriesForInvoiceAsync(connection, inv.Id, cancellationToken);
            inv.LoadSundries(sundries);
        }

        return invoices;
    }

    public async Task AddAsync(PurchaseInvoice invoice, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            await InsertInvoiceInternalAsync(transaction.Connection!, transaction, invoice, cancellationToken);
        }
        else
        {
            await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var localTx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await InsertInvoiceInternalAsync(connection, localTx, invoice, cancellationToken);
                await localTx.CommitAsync(cancellationToken);
            }
            catch
            {
                await localTx.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    public async Task UpdateAsync(PurchaseInvoice invoice, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            await UpdateInvoiceInternalAsync(transaction.Connection!, transaction, invoice, cancellationToken);
        }
        else
        {
            await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var localTx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await UpdateInvoiceInternalAsync(connection, localTx, invoice, cancellationToken);
                await localTx.CommitAsync(cancellationToken);
            }
            catch
            {
                await localTx.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private static async Task InsertInvoiceInternalAsync(DbConnection connection, DbTransaction transaction, PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        await using var cmd = (SqliteCommand)connection.CreateCommand();
        cmd.Transaction = (SqliteTransaction)transaction;
        cmd.CommandText = @"
            INSERT INTO PurchaseInvoices (
                Id, CompanyId, FinancialYearId, VoucherId, Series, InvoiceNumber,
                InvoiceDate, SupplierId, MaterialCentre, Narration,
                ItemSubtotal, SundrySubtotal, GrandTotal, Status,
                TransportName, GrRrNumber, TransportDate, VehicleNumber, StationFromPlace,
                CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            ) VALUES (
                @Id, @CompanyId, @FinancialYearId, @VoucherId, @Series, @InvoiceNumber,
                @InvoiceDate, @SupplierId, @MaterialCentre, @Narration,
                @ItemSubtotal, @SundrySubtotal, @GrandTotal, @Status,
                @TransportName, @GrRrNumber, @TransportDate, @VehicleNumber, @StationFromPlace,
                @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
            );
        ";
        AddInvoiceParameters(cmd, invoice);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        foreach (var line in invoice.Lines)
        {
            await using var lineCmd = (SqliteCommand)connection.CreateCommand();
            lineCmd.Transaction = (SqliteTransaction)transaction;
            lineCmd.CommandText = @"
                INSERT INTO PurchaseInvoiceItemLines (
                    Id, PurchaseInvoiceId, SequenceNumber, ItemId, Quantity, UnitId, Price, Amount
                ) VALUES (
                    @Id, @PurchaseInvoiceId, @SequenceNumber, @ItemId, @Quantity, @UnitId, @Price, @Amount
                );
            ";
            lineCmd.Parameters.AddWithValue("@Id", line.Id.ToString());
            lineCmd.Parameters.AddWithValue("@PurchaseInvoiceId", line.PurchaseInvoiceId.ToString());
            lineCmd.Parameters.AddWithValue("@SequenceNumber", line.SequenceNumber);
            lineCmd.Parameters.AddWithValue("@ItemId", line.ItemId.ToString());
            lineCmd.Parameters.AddWithValue("@Quantity", QuantityStorageHelper.ToStorageString(line.Quantity));
            lineCmd.Parameters.AddWithValue("@UnitId", line.UnitId.ToString());
            lineCmd.Parameters.AddWithValue("@Price", line.Price.ToMinorUnits());
            lineCmd.Parameters.AddWithValue("@Amount", line.Amount.ToMinorUnits());
            await lineCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var sundry in invoice.Sundries)
        {
            await using var sundryCmd = (SqliteCommand)connection.CreateCommand();
            sundryCmd.Transaction = (SqliteTransaction)transaction;
            sundryCmd.CommandText = @"
                INSERT INTO PurchaseInvoiceSundryLines (
                    Id, PurchaseInvoiceId, SequenceNumber, LedgerId, Percentage, Amount
                ) VALUES (
                    @Id, @PurchaseInvoiceId, @SequenceNumber, @LedgerId, @Percentage, @Amount
                );
            ";
            sundryCmd.Parameters.AddWithValue("@Id", sundry.Id.ToString());
            sundryCmd.Parameters.AddWithValue("@PurchaseInvoiceId", sundry.PurchaseInvoiceId.ToString());
            sundryCmd.Parameters.AddWithValue("@SequenceNumber", sundry.SequenceNumber);
            sundryCmd.Parameters.AddWithValue("@LedgerId", sundry.LedgerId.ToString());
            sundryCmd.Parameters.AddWithValue("@Percentage", (object?)sundry.Percentage ?? DBNull.Value);
            sundryCmd.Parameters.AddWithValue("@Amount", sundry.Amount.ToMinorUnits());
            await sundryCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdateInvoiceInternalAsync(DbConnection connection, DbTransaction transaction, PurchaseInvoice invoice, CancellationToken cancellationToken)
    {
        await using var cmd = (SqliteCommand)connection.CreateCommand();
        cmd.Transaction = (SqliteTransaction)transaction;
        cmd.CommandText = @"
            UPDATE PurchaseInvoices SET
                CompanyId = @CompanyId,
                FinancialYearId = @FinancialYearId,
                VoucherId = @VoucherId,
                Series = @Series,
                InvoiceNumber = @InvoiceNumber,
                InvoiceDate = @InvoiceDate,
                SupplierId = @SupplierId,
                MaterialCentre = @MaterialCentre,
                Narration = @Narration,
                ItemSubtotal = @ItemSubtotal,
                SundrySubtotal = @SundrySubtotal,
                GrandTotal = @GrandTotal,
                Status = @Status,
                TransportName = @TransportName,
                GrRrNumber = @GrRrNumber,
                TransportDate = @TransportDate,
                VehicleNumber = @VehicleNumber,
                StationFromPlace = @StationFromPlace,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE Id = @Id;
        ";
        AddInvoiceParameters(cmd, invoice);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<PurchaseInvoiceItemLine>> LoadLinesForInvoiceAsync(SqliteConnection connection, Guid invoiceId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, PurchaseInvoiceId, SequenceNumber, ItemId, Quantity, UnitId, Price, Amount
            FROM PurchaseInvoiceItemLines
            WHERE PurchaseInvoiceId = @PurchaseInvoiceId
            ORDER BY SequenceNumber ASC;
        ";
        cmd.Parameters.AddWithValue("@PurchaseInvoiceId", invoiceId.ToString());

        var lines = new List<PurchaseInvoiceItemLine>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            var pInvId = Guid.Parse(reader.GetString(1));
            var seq = reader.GetInt32(2);
            var itemId = Guid.Parse(reader.GetString(3));
            var qty = QuantityStorageHelper.FromStorageString(reader.GetString(4));
            var unitId = Guid.Parse(reader.GetString(5));
            var price = Money.FromMinorUnits(reader.GetInt64(6));

            var line = new PurchaseInvoiceItemLine(id, pInvId, seq, itemId, qty, unitId, price);
            lines.Add(line);
        }

        return lines;
    }

    private static async Task<List<PurchaseInvoiceSundryLine>> LoadSundriesForInvoiceAsync(SqliteConnection connection, Guid invoiceId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, PurchaseInvoiceId, SequenceNumber, LedgerId, Percentage, Amount
            FROM PurchaseInvoiceSundryLines
            WHERE PurchaseInvoiceId = @PurchaseInvoiceId
            ORDER BY SequenceNumber ASC;
        ";
        cmd.Parameters.AddWithValue("@PurchaseInvoiceId", invoiceId.ToString());

        var sundries = new List<PurchaseInvoiceSundryLine>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            var pInvId = Guid.Parse(reader.GetString(1));
            var seq = reader.GetInt32(2);
            var ledgerId = Guid.Parse(reader.GetString(3));
            decimal? pct = reader.IsDBNull(4) ? null : Convert.ToDecimal(reader.GetDouble(4));
            var amount = Money.FromMinorUnits(reader.GetInt64(5));

            var sundry = new PurchaseInvoiceSundryLine(id, pInvId, seq, ledgerId, amount, pct);
            sundries.Add(sundry);
        }

        return sundries;
    }

    private static PurchaseInvoice MapInvoiceFromReader(DbDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var fyId = Guid.Parse(reader.GetString(2));
        Guid? voucherId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3));
        var series = reader.GetString(4);
        var invoiceNumber = reader.GetString(5);
        var invoiceDate = DateOnly.Parse(reader.GetString(6));
        var supplierId = Guid.Parse(reader.GetString(7));
        var materialCentre = reader.GetString(8);
        var narration = reader.IsDBNull(9) ? null : reader.GetString(9);
        var status = (PurchaseInvoiceStatus)reader.GetInt32(13);

        string? transportName = reader.IsDBNull(14) ? null : reader.GetString(14);
        string? grRrNumber = reader.IsDBNull(15) ? null : reader.GetString(15);
        DateOnly? transportDate = reader.IsDBNull(16) ? null : DateOnly.Parse(reader.GetString(16));
        string? vehicleNumber = reader.IsDBNull(17) ? null : reader.GetString(17);
        string? station = reader.IsDBNull(18) ? null : reader.GetString(18);

        TransportDetails? transport = null;
        if (transportName != null || grRrNumber != null || transportDate.HasValue || vehicleNumber != null || station != null)
        {
            transport = new TransportDetails(transportName, grRrNumber, transportDate, vehicleNumber, station);
        }

        var invoice = new PurchaseInvoice(
            id,
            companyId,
            fyId,
            invoiceNumber,
            invoiceDate,
            supplierId,
            series,
            materialCentre,
            narration,
            status,
            voucherId,
            transport)
        {
            CreatedAt = DateTime.Parse(reader.GetString(19)),
            CreatedBy = reader.GetString(20),
            ModifiedAt = reader.IsDBNull(21) ? null : DateTime.Parse(reader.GetString(21)),
            ModifiedBy = reader.IsDBNull(22) ? null : reader.GetString(22)
        };

        return invoice;
    }

    private static void AddInvoiceParameters(SqliteCommand cmd, PurchaseInvoice invoice)
    {
        cmd.Parameters.AddWithValue("@Id", invoice.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", invoice.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@FinancialYearId", invoice.FinancialYearId.ToString());
        cmd.Parameters.AddWithValue("@VoucherId", (object?)invoice.VoucherId?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Series", invoice.Series);
        cmd.Parameters.AddWithValue("@InvoiceNumber", invoice.InvoiceNumber);
        cmd.Parameters.AddWithValue("@InvoiceDate", invoice.InvoiceDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@SupplierId", invoice.SupplierId.ToString());
        cmd.Parameters.AddWithValue("@MaterialCentre", invoice.MaterialCentre);
        cmd.Parameters.AddWithValue("@Narration", (object?)invoice.Narration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ItemSubtotal", invoice.ItemSubtotal.ToMinorUnits());
        cmd.Parameters.AddWithValue("@SundrySubtotal", invoice.SundrySubtotal.ToMinorUnits());
        cmd.Parameters.AddWithValue("@GrandTotal", invoice.GrandTotal.ToMinorUnits());
        cmd.Parameters.AddWithValue("@Status", (int)invoice.Status);

        cmd.Parameters.AddWithValue("@TransportName", (object?)invoice.Transport?.TransportName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GrRrNumber", (object?)invoice.Transport?.GrRrNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TransportDate", (object?)invoice.Transport?.TransportDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@VehicleNumber", (object?)invoice.Transport?.VehicleNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StationFromPlace", (object?)invoice.Transport?.StationFromPlace ?? DBNull.Value);

        cmd.Parameters.AddWithValue("@CreatedAt", invoice.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedBy", invoice.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", (object?)invoice.ModifiedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)invoice.ModifiedBy ?? DBNull.Value);
    }
}

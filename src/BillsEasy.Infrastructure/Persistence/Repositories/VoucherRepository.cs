using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class VoucherRepository : IVoucherRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public VoucherRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Voucher?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, FinancialYearId, VoucherNumber, Type, Status,
                   VoucherDate, ReferenceNumber, Narration, TotalAmount,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, PostedAt, PostedBy
            FROM Vouchers
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var voucher = MapVoucherFromReader(reader);
        await reader.CloseAsync();

        var lines = await LoadLinesForVoucherAsync(connection, voucher.Id, cancellationToken);
        voucher.LoadLines(lines);

        return voucher;
    }

    public async Task<Voucher?> GetByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, FinancialYearId, VoucherNumber, Type, Status,
                   VoucherDate, ReferenceNumber, Narration, TotalAmount,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, PostedAt, PostedBy
            FROM Vouchers
            WHERE CompanyId = @CompanyId 
              AND FinancialYearId = @FinancialYearId 
              AND VoucherNumber = @VoucherNumber COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.ToString());
        cmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var voucher = MapVoucherFromReader(reader);
        await reader.CloseAsync();

        var lines = await LoadLinesForVoucherAsync(connection, voucher.Id, cancellationToken);
        voucher.LoadLines(lines);

        return voucher;
    }

    public async Task<IReadOnlyList<Voucher>> GetAllAsync(
        Guid companyId,
        Guid? financialYearId = null,
        VoucherType? type = null,
        VoucherStatus? status = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? ledgerId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var query = @"
            SELECT Id, CompanyId, FinancialYearId, VoucherNumber, Type, Status,
                   VoucherDate, ReferenceNumber, Narration, TotalAmount,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, PostedAt, PostedBy
            FROM Vouchers
            WHERE CompanyId = @CompanyId
        ";

        if (financialYearId.HasValue)
            query += " AND FinancialYearId = @FinancialYearId";

        if (type.HasValue)
            query += " AND Type = @Type";

        if (status.HasValue)
            query += " AND Status = @Status";

        if (fromDate.HasValue)
            query += " AND VoucherDate >= @FromDate";

        if (toDate.HasValue)
            query += " AND VoucherDate <= @ToDate";

        if (ledgerId.HasValue)
            query += " AND Id IN (SELECT VoucherId FROM VoucherLines WHERE LedgerId = @LedgerId)";

        query += " ORDER BY VoucherDate DESC, CreatedAt DESC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        if (financialYearId.HasValue)
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.Value.ToString());

        if (type.HasValue)
            cmd.Parameters.AddWithValue("@Type", (int)type.Value);

        if (status.HasValue)
            cmd.Parameters.AddWithValue("@Status", (int)status.Value);

        if (fromDate.HasValue)
            cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.ToString("yyyy-MM-dd"));

        if (toDate.HasValue)
            cmd.Parameters.AddWithValue("@ToDate", toDate.Value.ToString("yyyy-MM-dd"));

        if (ledgerId.HasValue)
            cmd.Parameters.AddWithValue("@LedgerId", ledgerId.Value.ToString());

        var vouchers = new List<Voucher>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                vouchers.Add(MapVoucherFromReader(reader));
            }
        }

        if (vouchers.Count == 0)
            return vouchers;

        var voucherIds = vouchers.Select(v => $"'{v.Id}'").ToList();
        var linesLookup = new Dictionary<Guid, List<VoucherLine>>();

        // Chunk if vouchers count is large, or query batch
        const int chunkSize = 500;
        for (int i = 0; i < voucherIds.Count; i += chunkSize)
        {
            var chunk = voucherIds.Skip(i).Take(chunkSize);
            var inClause = string.Join(",", chunk);

            await using var linesCmd = connection.CreateCommand();
            linesCmd.CommandText = $@"
                SELECT Id, VoucherId, LedgerId, SequenceNumber, Type, Amount, Narration
                FROM VoucherLines
                WHERE VoucherId IN ({inClause})
                ORDER BY SequenceNumber ASC;
            ";

            await using var linesReader = await linesCmd.ExecuteReaderAsync(cancellationToken);
            while (await linesReader.ReadAsync(cancellationToken))
            {
                var line = MapVoucherLineFromReader(linesReader);
                if (!linesLookup.TryGetValue(line.VoucherId, out var lineList))
                {
                    lineList = new List<VoucherLine>();
                    linesLookup[line.VoucherId] = lineList;
                }
                lineList.Add(line);
            }
        }

        foreach (var voucher in vouchers)
        {
            if (linesLookup.TryGetValue(voucher.Id, out var lines))
            {
                voucher.LoadLines(lines);
            }
        }

        return vouchers;
    }

    public Task<bool> ExistsByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => ExistsByNumberAsync(companyId, financialYearId, voucherNumber, excludeId, null, cancellationToken);

    public async Task<bool> ExistsByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, Guid? excludeId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        SqliteConnection? localConnection = null;
        var connection = (SqliteConnection?)(transaction?.Connection);
        if (connection == null)
        {
            localConnection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
            connection = localConnection;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            if (transaction != null)
                cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = @"
                SELECT COUNT(1) FROM Vouchers
                WHERE CompanyId = @CompanyId 
                  AND FinancialYearId = @FinancialYearId 
                  AND VoucherNumber = @VoucherNumber COLLATE NOCASE
                  AND (@ExcludeId IS NULL OR Id != @ExcludeId);
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId.ToString());
            cmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber.Trim());
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.HasValue ? (object)excludeId.Value.ToString() : DBNull.Value);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }
        finally
        {
            if (localConnection != null)
                await localConnection.DisposeAsync();
        }
    }

    public Task<(VoucherLine Line, Guid CompanyId)?> GetLineWithCompanyAsync(Guid voucherLineId, CancellationToken cancellationToken = default)
        => GetLineWithCompanyAsync(voucherLineId, null, cancellationToken);

    public async Task<(VoucherLine Line, Guid CompanyId)?> GetLineWithCompanyAsync(Guid voucherLineId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        SqliteConnection? localConnection = null;
        var connection = (SqliteConnection?)(transaction?.Connection);
        if (connection == null)
        {
            localConnection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
            connection = localConnection;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            if (transaction != null)
                cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = @"
                SELECT vl.Id, vl.VoucherId, vl.LedgerId, vl.SequenceNumber, vl.Type, vl.Amount, vl.Narration, v.CompanyId
                FROM VoucherLines vl
                INNER JOIN Vouchers v ON vl.VoucherId = v.Id
                WHERE vl.Id = @VoucherLineId;
            ";
            cmd.Parameters.AddWithValue("@VoucherLineId", voucherLineId.ToString());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var line = MapVoucherLineFromReader(reader);
            var companyId = Guid.Parse(reader.GetString(7));

            return (line, companyId);
        }
        finally
        {
            if (localConnection != null)
                await localConnection.DisposeAsync();
        }
    }

    public Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default)
        => AddAsync(voucher, null, cancellationToken);

    public async Task AddAsync(Voucher voucher, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            await InsertVoucherInternalAsync(transaction.Connection!, transaction, voucher, cancellationToken);
        }
        else
        {
            await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var localTx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await InsertVoucherInternalAsync(connection, localTx, voucher, cancellationToken);
                await localTx.CommitAsync(cancellationToken);
            }
            catch
            {
                await localTx.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private static async Task InsertVoucherInternalAsync(DbConnection connection, DbTransaction transaction, Voucher voucher, CancellationToken cancellationToken)
    {
        await using (var cmd = (SqliteCommand)connection.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = @"
                INSERT INTO Vouchers (
                    Id, CompanyId, FinancialYearId, VoucherNumber, Type, Status,
                    VoucherDate, ReferenceNumber, Narration, TotalAmount,
                    CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, PostedAt, PostedBy
                ) VALUES (
                    @Id, @CompanyId, @FinancialYearId, @VoucherNumber, @Type, @Status,
                    @VoucherDate, @ReferenceNumber, @Narration, @TotalAmount,
                    @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy, @PostedAt, @PostedBy
                );
            ";
            AddVoucherParameters(cmd, voucher);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var line in voucher.Lines)
        {
            await using var lineCmd = (SqliteCommand)connection.CreateCommand();
            lineCmd.Transaction = (SqliteTransaction)transaction;
            lineCmd.CommandText = @"
                INSERT INTO VoucherLines (
                    Id, VoucherId, LedgerId, SequenceNumber, Type, Amount, Narration
                ) VALUES (
                    @Id, @VoucherId, @LedgerId, @SequenceNumber, @Type, @Amount, @Narration
                );
            ";
            AddVoucherLineParameters(lineCmd, line);
            await lineCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task UpdateAsync(Voucher voucher, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE Vouchers SET
                        CompanyId = @CompanyId,
                        FinancialYearId = @FinancialYearId,
                        VoucherNumber = @VoucherNumber,
                        Type = @Type,
                        Status = @Status,
                        VoucherDate = @VoucherDate,
                        ReferenceNumber = @ReferenceNumber,
                        Narration = @Narration,
                        TotalAmount = @TotalAmount,
                        ModifiedAt = @ModifiedAt,
                        ModifiedBy = @ModifiedBy,
                        PostedAt = @PostedAt,
                        PostedBy = @PostedBy
                    WHERE Id = @Id AND CompanyId = @CompanyId;
                ";
                AddVoucherParameters(cmd, voucher);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (voucher.Lines.Count > 0)
            {
                await using var delCmd = connection.CreateCommand();
                delCmd.Transaction = transaction;
                var paramNames = voucher.Lines.Select((_, idx) => $"@LineId{idx}").ToList();
                delCmd.CommandText = $"DELETE FROM VoucherLines WHERE VoucherId = @VoucherId AND Id NOT IN ({string.Join(", ", paramNames)});";
                delCmd.Parameters.AddWithValue("@VoucherId", voucher.Id.ToString());
                for (int i = 0; i < voucher.Lines.Count; i++)
                {
                    delCmd.Parameters.AddWithValue(paramNames[i], voucher.Lines[i].Id.ToString());
                }
                await delCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var delCmd = connection.CreateCommand();
                delCmd.Transaction = transaction;
                delCmd.CommandText = "DELETE FROM VoucherLines WHERE VoucherId = @VoucherId;";
                delCmd.Parameters.AddWithValue("@VoucherId", voucher.Id.ToString());
                await delCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var line in voucher.Lines)
            {
                await using var lineCmd = connection.CreateCommand();
                lineCmd.Transaction = transaction;
                lineCmd.CommandText = @"
                    INSERT INTO VoucherLines (
                        Id, VoucherId, LedgerId, SequenceNumber, Type, Amount, Narration
                    ) VALUES (
                        @Id, @VoucherId, @LedgerId, @SequenceNumber, @Type, @Amount, @Narration
                    )
                    ON CONFLICT(Id) DO UPDATE SET
                        LedgerId = excluded.LedgerId,
                        SequenceNumber = excluded.SequenceNumber,
                        Type = excluded.Type,
                        Amount = excluded.Amount,
                        Narration = excluded.Narration;
                ";
                AddVoucherLineParameters(lineCmd, line);
                await lineCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Vouchers WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<VoucherLine>> LoadLinesForVoucherAsync(SqliteConnection connection, Guid voucherId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, VoucherId, LedgerId, SequenceNumber, Type, Amount, Narration
            FROM VoucherLines
            WHERE VoucherId = @VoucherId
            ORDER BY SequenceNumber ASC;
        ";
        cmd.Parameters.AddWithValue("@VoucherId", voucherId.ToString());

        var lines = new List<VoucherLine>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(MapVoucherLineFromReader(reader));
        }

        return lines;
    }

    private static Voucher MapVoucherFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var financialYearId = Guid.Parse(reader.GetString(2));
        var voucherNumber = reader.GetString(3);
        var type = (VoucherType)reader.GetInt32(4);
        var status = (VoucherStatus)reader.GetInt32(5);
        var voucherDate = DateOnly.Parse(reader.GetString(6));
        var referenceNumber = reader.IsDBNull(7) ? null : reader.GetString(7);
        var narration = reader.IsDBNull(8) ? null : reader.GetString(8);
        var totalAmount = Money.FromMinorUnits(reader.GetInt64(9));
        var createdAt = DateTime.Parse(reader.GetString(10));
        var createdBy = reader.GetString(11);
        var modifiedAt = reader.IsDBNull(12) ? null : (DateTime?)DateTime.Parse(reader.GetString(12));
        var modifiedBy = reader.IsDBNull(13) ? null : reader.GetString(13);
        var postedAt = reader.IsDBNull(14) ? null : (DateTime?)DateTime.Parse(reader.GetString(14));
        var postedBy = reader.IsDBNull(15) ? null : reader.GetString(15);

        var voucher = new Voucher(
            id,
            companyId,
            financialYearId,
            voucherNumber,
            type,
            voucherDate,
            status,
            referenceNumber,
            narration,
            postedAt,
            postedBy,
            totalAmount)
        {
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            ModifiedAt = modifiedAt,
            ModifiedBy = modifiedBy
        };

        return voucher;
    }

    private static VoucherLine MapVoucherLineFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var voucherId = Guid.Parse(reader.GetString(1));
        var ledgerId = Guid.Parse(reader.GetString(2));
        var sequenceNumber = reader.GetInt32(3);
        var type = (DebitCreditType)reader.GetInt32(4);
        var amount = Money.FromMinorUnits(reader.GetInt64(5));
        var narration = reader.IsDBNull(6) ? null : reader.GetString(6);

        return new VoucherLine(id, voucherId, ledgerId, sequenceNumber, type, amount, narration);
    }

    private static void AddVoucherParameters(SqliteCommand cmd, Voucher voucher)
    {
        cmd.Parameters.AddWithValue("@Id", voucher.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", voucher.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@FinancialYearId", voucher.FinancialYearId.ToString());
        cmd.Parameters.AddWithValue("@VoucherNumber", voucher.VoucherNumber);
        cmd.Parameters.AddWithValue("@Type", (int)voucher.Type);
        cmd.Parameters.AddWithValue("@Status", (int)voucher.Status);
        cmd.Parameters.AddWithValue("@VoucherDate", voucher.VoucherDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@ReferenceNumber", (object?)voucher.ReferenceNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Narration", (object?)voucher.Narration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TotalAmount", voucher.TotalAmount.ToMinorUnits());
        cmd.Parameters.AddWithValue("@CreatedAt", voucher.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@CreatedBy", voucher.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", voucher.ModifiedAt.HasValue ? (object)voucher.ModifiedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)voucher.ModifiedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PostedAt", voucher.PostedAt.HasValue ? (object)voucher.PostedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@PostedBy", (object?)voucher.PostedBy ?? DBNull.Value);
    }

    private static void AddVoucherLineParameters(SqliteCommand cmd, VoucherLine line)
    {
        cmd.Parameters.AddWithValue("@Id", line.Id.ToString());
        cmd.Parameters.AddWithValue("@VoucherId", line.VoucherId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", line.LedgerId.ToString());
        cmd.Parameters.AddWithValue("@SequenceNumber", line.SequenceNumber);
        cmd.Parameters.AddWithValue("@Type", (int)line.Type);
        cmd.Parameters.AddWithValue("@Amount", line.Amount.ToMinorUnits());
        cmd.Parameters.AddWithValue("@Narration", (object?)line.Narration ?? DBNull.Value);
    }
}

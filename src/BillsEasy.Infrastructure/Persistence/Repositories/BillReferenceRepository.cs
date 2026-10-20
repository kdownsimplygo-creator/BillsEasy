using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class BillReferenceRepository : IBillReferenceRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public BillReferenceRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BillReference?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, LedgerId, ReferenceNumber, ReferenceType, Status,
                   BillDate, DueDate, OriginalAmount, EntryType, SourceVoucherLineId,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM BillReferences
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapBillReferenceFromReader(reader);
    }

    public async Task<BillReference?> GetByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, LedgerId, ReferenceNumber, ReferenceType, Status,
                   BillDate, DueDate, OriginalAmount, EntryType, SourceVoucherLineId,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM BillReferences
            WHERE CompanyId = @CompanyId 
              AND LedgerId = @LedgerId
              AND ReferenceNumber = @ReferenceNumber COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());
        cmd.Parameters.AddWithValue("@ReferenceNumber", referenceNumber.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapBillReferenceFromReader(reader);
    }

    public async Task<BillReference?> GetBySourceVoucherLineIdAsync(Guid companyId, Guid sourceVoucherLineId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, LedgerId, ReferenceNumber, ReferenceType, Status,
                   BillDate, DueDate, OriginalAmount, EntryType, SourceVoucherLineId,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM BillReferences
            WHERE CompanyId = @CompanyId AND SourceVoucherLineId = @SourceVoucherLineId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@SourceVoucherLineId", sourceVoucherLineId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapBillReferenceFromReader(reader);
    }

    public async Task<IReadOnlyList<BillReferenceWithBalance>> GetActiveBillsByLedgerAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT br.Id, br.CompanyId, br.LedgerId, br.ReferenceNumber, br.ReferenceType, br.Status,
                   br.BillDate, br.DueDate, br.OriginalAmount, br.EntryType, br.SourceVoucherLineId,
                   br.CreatedAt, br.CreatedBy, br.ModifiedAt, br.ModifiedBy,
                   COALESCE(SUM(ba.AllocatedAmount), 0) AS TotalAllocated,
                   (br.OriginalAmount - COALESCE(SUM(ba.AllocatedAmount), 0)) AS OutstandingAmount
            FROM BillReferences br
            LEFT JOIN BillAllocations ba ON br.Id = ba.BillReferenceId
            WHERE br.CompanyId = @CompanyId 
              AND br.LedgerId = @LedgerId
              AND br.Status = 1
            GROUP BY br.Id
            ORDER BY br.BillDate ASC, br.CreatedAt ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());

        var results = new List<BillReferenceWithBalance>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var bill = MapBillReferenceFromReader(reader);
            var totalAllocatedPaise = reader.GetInt64(15);
            var outstandingPaise = reader.GetInt64(16);

            results.Add(new BillReferenceWithBalance(
                bill,
                Money.FromMinorUnits(totalAllocatedPaise),
                Money.FromMinorUnits(outstandingPaise)));
        }

        return results;
    }

    public async Task<Money> GetOutstandingBalanceAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT (br.OriginalAmount - COALESCE(SUM(ba.AllocatedAmount), 0)) AS OutstandingAmount
            FROM BillReferences br
            LEFT JOIN BillAllocations ba ON br.Id = ba.BillReferenceId
            WHERE br.CompanyId = @CompanyId 
              AND br.Id = @BillReferenceId
              AND br.Status = 1
            GROUP BY br.Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@BillReferenceId", billReferenceId.ToString());

        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        if (scalar == null || scalar == DBNull.Value)
            return Money.Zero();

        return Money.FromMinorUnits(Convert.ToInt64(scalar));
    }

    public async Task<Money> GetTotalAllocatedAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(SUM(ba.AllocatedAmount), 0) AS TotalAllocated
            FROM BillReferences br
            LEFT JOIN BillAllocations ba ON br.Id = ba.BillReferenceId
            WHERE br.CompanyId = @CompanyId 
              AND br.Id = @BillReferenceId
            GROUP BY br.Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@BillReferenceId", billReferenceId.ToString());

        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        if (scalar == null || scalar == DBNull.Value)
            return Money.Zero();

        return Money.FromMinorUnits(Convert.ToInt64(scalar));
    }

    public Task<bool> ExistsByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => ExistsByReferenceNumberAsync(companyId, ledgerId, referenceNumber, excludeId, null, cancellationToken);

    public async Task<bool> ExistsByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, Guid? excludeId, DbTransaction? transaction, CancellationToken cancellationToken = default)
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
                SELECT COUNT(1) FROM BillReferences
                WHERE CompanyId = @CompanyId 
                  AND LedgerId = @LedgerId
                  AND ReferenceNumber = @ReferenceNumber COLLATE NOCASE
                  AND (@ExcludeId IS NULL OR Id != @ExcludeId);
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());
            cmd.Parameters.AddWithValue("@ReferenceNumber", referenceNumber.Trim());
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

    public Task AddAsync(BillReference billReference, CancellationToken cancellationToken = default)
        => AddAsync(billReference, null, cancellationToken);

    public async Task AddAsync(BillReference billReference, DbTransaction? transaction, CancellationToken cancellationToken = default)
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
                INSERT INTO BillReferences (
                    Id, CompanyId, LedgerId, ReferenceNumber, ReferenceType, Status,
                    BillDate, DueDate, OriginalAmount, EntryType, SourceVoucherLineId,
                    CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyId, @LedgerId, @ReferenceNumber, @ReferenceType, @Status,
                    @BillDate, @DueDate, @OriginalAmount, @EntryType, @SourceVoucherLineId,
                    @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";
            AddParameters(cmd, billReference);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (localConnection != null)
                await localConnection.DisposeAsync();
        }
    }

    public async Task UpdateAsync(BillReference billReference, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE BillReferences SET
                CompanyId = @CompanyId,
                LedgerId = @LedgerId,
                ReferenceNumber = @ReferenceNumber,
                ReferenceType = @ReferenceType,
                Status = @Status,
                BillDate = @BillDate,
                DueDate = @DueDate,
                OriginalAmount = @OriginalAmount,
                EntryType = @EntryType,
                SourceVoucherLineId = @SourceVoucherLineId,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        AddParameters(cmd, billReference);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM BillReferences WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, BillReference bill)
    {
        cmd.Parameters.AddWithValue("@Id", bill.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", bill.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", bill.LedgerId.ToString());
        cmd.Parameters.AddWithValue("@ReferenceNumber", bill.ReferenceNumber);
        cmd.Parameters.AddWithValue("@ReferenceType", (int)bill.ReferenceType);
        cmd.Parameters.AddWithValue("@Status", (int)bill.Status);
        cmd.Parameters.AddWithValue("@BillDate", bill.BillDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@DueDate", bill.DueDate.HasValue ? (object)bill.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("@OriginalAmount", bill.OriginalAmount.ToMinorUnits());
        cmd.Parameters.AddWithValue("@EntryType", (int)bill.EntryType);
        cmd.Parameters.AddWithValue("@SourceVoucherLineId", bill.SourceVoucherLineId.HasValue ? (object)bill.SourceVoucherLineId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", bill.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@CreatedBy", bill.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", bill.ModifiedAt.HasValue ? (object)bill.ModifiedAt.Value.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", string.IsNullOrEmpty(bill.ModifiedBy) ? DBNull.Value : (object)bill.ModifiedBy);
    }

    private static BillReference MapBillReferenceFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var ledgerId = Guid.Parse(reader.GetString(2));
        var referenceNumber = reader.GetString(3);
        var referenceType = (BillReferenceType)reader.GetInt32(4);
        var status = (BillReferenceStatus)reader.GetInt32(5);
        var billDate = DateOnly.Parse(reader.GetString(6), CultureInfo.InvariantCulture);
        var dueDate = reader.IsDBNull(7) ? (DateOnly?)null : DateOnly.Parse(reader.GetString(7), CultureInfo.InvariantCulture);
        var originalAmountPaise = reader.GetInt64(8);
        var entryType = (DebitCreditType)reader.GetInt32(9);
        var sourceVoucherLineId = reader.IsDBNull(10) ? (Guid?)null : Guid.Parse(reader.GetString(10));
        var createdAt = DateTime.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var createdBy = reader.GetString(12);
        var modifiedAt = reader.IsDBNull(13) ? (DateTime?)null : DateTime.Parse(reader.GetString(13), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var modifiedBy = reader.IsDBNull(14) ? null : reader.GetString(14);

        return new BillReference(
            id,
            companyId,
            ledgerId,
            referenceNumber,
            referenceType,
            billDate,
            Money.FromMinorUnits(originalAmountPaise),
            entryType,
            dueDate,
            status,
            sourceVoucherLineId)
        {
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            ModifiedAt = modifiedAt,
            ModifiedBy = modifiedBy
        };
    }
}

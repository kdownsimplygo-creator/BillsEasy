using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class BillAllocationRepository : IBillAllocationRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public BillAllocationRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<BillAllocation>> GetByVoucherLineIdAsync(Guid companyId, Guid voucherLineId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, VoucherLineId, BillReferenceId, AllocatedAmount, CreatedAt, CreatedBy
            FROM BillAllocations
            WHERE CompanyId = @CompanyId AND VoucherLineId = @VoucherLineId
            ORDER BY CreatedAt ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@VoucherLineId", voucherLineId.ToString());

        var allocations = new List<BillAllocation>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            allocations.Add(MapBillAllocationFromReader(reader));
        }

        return allocations;
    }

    public async Task<IReadOnlyList<BillAllocation>> GetByBillReferenceIdAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, VoucherLineId, BillReferenceId, AllocatedAmount, CreatedAt, CreatedBy
            FROM BillAllocations
            WHERE CompanyId = @CompanyId AND BillReferenceId = @BillReferenceId
            ORDER BY CreatedAt ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@BillReferenceId", billReferenceId.ToString());

        var allocations = new List<BillAllocation>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            allocations.Add(MapBillAllocationFromReader(reader));
        }

        return allocations;
    }

    public async Task AddAsync(BillAllocation allocation, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO BillAllocations (
                Id, CompanyId, VoucherLineId, BillReferenceId, AllocatedAmount, CreatedAt, CreatedBy
            ) VALUES (
                @Id, @CompanyId, @VoucherLineId, @BillReferenceId, @AllocatedAmount, @CreatedAt, @CreatedBy
            );
        ";
        AddParameters(cmd, allocation);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<BillAllocation> allocations, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var allocation in allocations)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO BillAllocations (
                        Id, CompanyId, VoucherLineId, BillReferenceId, AllocatedAmount, CreatedAt, CreatedBy
                    ) VALUES (
                        @Id, @CompanyId, @VoucherLineId, @BillReferenceId, @AllocatedAmount, @CreatedAt, @CreatedBy
                    );
                ";
                AddParameters(cmd, allocation);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteByVoucherLineIdAsync(Guid companyId, Guid voucherLineId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM BillAllocations WHERE CompanyId = @CompanyId AND VoucherLineId = @VoucherLineId;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@VoucherLineId", voucherLineId.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM BillAllocations WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, BillAllocation allocation)
    {
        cmd.Parameters.AddWithValue("@Id", allocation.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", allocation.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@VoucherLineId", allocation.VoucherLineId.ToString());
        cmd.Parameters.AddWithValue("@BillReferenceId", allocation.BillReferenceId.ToString());
        cmd.Parameters.AddWithValue("@AllocatedAmount", allocation.AllocatedAmount.ToMinorUnits());
        cmd.Parameters.AddWithValue("@CreatedAt", allocation.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@CreatedBy", allocation.CreatedBy);
    }

    private static BillAllocation MapBillAllocationFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var voucherLineId = Guid.Parse(reader.GetString(2));
        var billReferenceId = Guid.Parse(reader.GetString(3));
        var allocatedAmountPaise = reader.GetInt64(4);
        var createdAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var createdBy = reader.GetString(6);

        return new BillAllocation(
            id,
            companyId,
            voucherLineId,
            billReferenceId,
            Money.FromMinorUnits(allocatedAmountPaise),
            createdAt,
            createdBy);
    }
}

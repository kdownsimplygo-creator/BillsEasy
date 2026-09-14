using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class FinancialYearRepository : IFinancialYearRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public FinancialYearRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, YearName, StartDate, EndDate, IsActive, IsLocked, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM FinancialYears WHERE CompanyId = @CompanyId ORDER BY StartDate ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<FinancialYear>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fy = new FinancialYear(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                DateOnly.Parse(reader.GetString(3)),
                DateOnly.Parse(reader.GetString(4)),
                reader.GetInt32(5) == 1,
                reader.GetInt32(6) == 1)
            {
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                CreatedBy = reader.GetString(8),
                ModifiedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
                ModifiedBy = reader.IsDBNull(10) ? null : reader.GetString(10)
            };
            list.Add(fy);
        }

        return list.AsReadOnly();
    }

    public async Task<FinancialYear?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, YearName, StartDate, EndDate, IsActive, IsLocked, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM FinancialYears WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new FinancialYear(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            DateOnly.Parse(reader.GetString(3)),
            DateOnly.Parse(reader.GetString(4)),
            reader.GetInt32(5) == 1,
            reader.GetInt32(6) == 1)
        {
            CreatedAt = DateTime.Parse(reader.GetString(7)),
            CreatedBy = reader.GetString(8),
            ModifiedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
            ModifiedBy = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    public async Task<FinancialYear?> GetActiveFinancialYearAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, YearName, StartDate, EndDate, IsActive, IsLocked, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM FinancialYears WHERE CompanyId = @CompanyId AND IsActive = 1 LIMIT 1;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new FinancialYear(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            DateOnly.Parse(reader.GetString(3)),
            DateOnly.Parse(reader.GetString(4)),
            true,
            reader.GetInt32(6) == 1)
        {
            CreatedAt = DateTime.Parse(reader.GetString(7)),
            CreatedBy = reader.GetString(8),
            ModifiedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
            ModifiedBy = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    public async Task AddAsync(FinancialYear financialYear, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (financialYear.IsActive)
            {
                await using var deactCmd = connection.CreateCommand();
                deactCmd.Transaction = (SqliteTransaction)transaction;
                deactCmd.CommandText = "UPDATE FinancialYears SET IsActive = 0 WHERE CompanyId = @CompanyId;";
                deactCmd.Parameters.AddWithValue("@CompanyId", financialYear.CompanyId.ToString());
                await deactCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = @"
                INSERT INTO FinancialYears (
                    Id, CompanyId, YearName, StartDate, EndDate, IsActive, IsLocked, CreatedAt, CreatedBy
                ) VALUES (
                    @Id, @CompanyId, @YearName, @StartDate, @EndDate, @IsActive, @IsLocked, @CreatedAt, @CreatedBy
                );
            ";
            cmd.Parameters.AddWithValue("@Id", financialYear.Id.ToString());
            cmd.Parameters.AddWithValue("@CompanyId", financialYear.CompanyId.ToString());
            cmd.Parameters.AddWithValue("@YearName", financialYear.YearName);
            cmd.Parameters.AddWithValue("@StartDate", financialYear.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@EndDate", financialYear.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@IsActive", financialYear.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@IsLocked", financialYear.IsLocked ? 1 : 0);
            cmd.Parameters.AddWithValue("@CreatedAt", financialYear.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@CreatedBy", financialYear.CreatedBy);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetActiveAsync(Guid companyId, Guid financialYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var deactCmd = connection.CreateCommand();
            deactCmd.Transaction = (SqliteTransaction)transaction;
            deactCmd.CommandText = "UPDATE FinancialYears SET IsActive = 0 WHERE CompanyId = @CompanyId;";
            deactCmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            await deactCmd.ExecuteNonQueryAsync(cancellationToken);

            await using var actCmd = connection.CreateCommand();
            actCmd.Transaction = (SqliteTransaction)transaction;
            actCmd.CommandText = "UPDATE FinancialYears SET IsActive = 1 WHERE CompanyId = @CompanyId AND Id = @Id;";
            actCmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            actCmd.Parameters.AddWithValue("@Id", financialYearId.ToString());
            await actCmd.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

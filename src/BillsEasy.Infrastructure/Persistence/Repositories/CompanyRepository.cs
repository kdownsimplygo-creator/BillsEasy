using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public CompanyRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyName, PrintName, ShortName, Country, StateName, StateCode,
                   FinancialYearBeginning, BooksBeginningFrom, CurrencySymbol, CurrencyFormalName,
                   GSTIN, GSTRegistrationType, IsSecurityEnabled, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Companies WHERE Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var company = ReadCompany(reader);
        await LoadFinancialYearsAsync(connection, company, cancellationToken);
        return company;
    }

    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyName, PrintName, ShortName, Country, StateName, StateCode,
                   FinancialYearBeginning, BooksBeginningFrom, CurrencySymbol, CurrencyFormalName,
                   GSTIN, GSTRegistrationType, IsSecurityEnabled, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Companies ORDER BY CompanyName ASC;
        ";

        var companies = new List<Company>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                companies.Add(ReadCompany(reader));
            }
        }

        foreach (var company in companies)
        {
            await LoadFinancialYearsAsync(connection, company, cancellationToken);
        }

        return companies.AsReadOnly();
    }

    public async Task<bool> ExistsByNameAsync(string companyName, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        if (excludeId.HasValue)
        {
            cmd.CommandText = "SELECT COUNT(1) FROM Companies WHERE LOWER(CompanyName) = LOWER(@Name) AND Id != @ExcludeId;";
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value.ToString());
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(1) FROM Companies WHERE LOWER(CompanyName) = LOWER(@Name);";
        }
        cmd.Parameters.AddWithValue("@Name", companyName.Trim());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = @"
                INSERT INTO Companies (
                    Id, CompanyName, PrintName, ShortName, Country, StateName, StateCode,
                    FinancialYearBeginning, BooksBeginningFrom, CurrencySymbol, CurrencyFormalName,
                    GSTIN, GSTRegistrationType, IsSecurityEnabled, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyName, @PrintName, @ShortName, @Country, @StateName, @StateCode,
                    @FinancialYearBeginning, @BooksBeginningFrom, @CurrencySymbol, @CurrencyFormalName,
                    @GSTIN, @GSTRegistrationType, @IsSecurityEnabled, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";

            AddCompanyParameters(cmd, company);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            foreach (var fy in company.FinancialYears)
            {
                await using var fyCmd = connection.CreateCommand();
                fyCmd.Transaction = (SqliteTransaction)transaction;
                fyCmd.CommandText = @"
                    INSERT INTO FinancialYears (
                        Id, CompanyId, YearName, StartDate, EndDate, IsActive, IsLocked, CreatedAt, CreatedBy
                    ) VALUES (
                        @Id, @CompanyId, @YearName, @StartDate, @EndDate, @IsActive, @IsLocked, @CreatedAt, @CreatedBy
                    );
                ";
                fyCmd.Parameters.AddWithValue("@Id", fy.Id.ToString());
                fyCmd.Parameters.AddWithValue("@CompanyId", company.Id.ToString());
                fyCmd.Parameters.AddWithValue("@YearName", fy.YearName);
                fyCmd.Parameters.AddWithValue("@StartDate", fy.StartDate.ToString("yyyy-MM-dd"));
                fyCmd.Parameters.AddWithValue("@EndDate", fy.EndDate.ToString("yyyy-MM-dd"));
                fyCmd.Parameters.AddWithValue("@IsActive", fy.IsActive ? 1 : 0);
                fyCmd.Parameters.AddWithValue("@IsLocked", fy.IsLocked ? 1 : 0);
                fyCmd.Parameters.AddWithValue("@CreatedAt", fy.CreatedAt.ToString("O"));
                fyCmd.Parameters.AddWithValue("@CreatedBy", fy.CreatedBy);

                await fyCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(Company company, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Companies SET
                CompanyName = @CompanyName,
                PrintName = @PrintName,
                ShortName = @ShortName,
                Country = @Country,
                StateName = @StateName,
                StateCode = @StateCode,
                GSTIN = @GSTIN,
                GSTRegistrationType = @GSTRegistrationType,
                IsSecurityEnabled = @IsSecurityEnabled,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE Id = @Id;
        ";

        AddCompanyParameters(cmd, company);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> HasTransactionsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Vouchers';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM Vouchers v
            INNER JOIN FinancialYears fy ON v.FinancialYearId = fy.Id
            WHERE fy.CompanyId = @CompanyId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            string? companyName = null;
            await using (var nameCmd = connection.CreateCommand())
            {
                nameCmd.Transaction = (SqliteTransaction)transaction;
                nameCmd.CommandText = "SELECT CompanyName FROM Companies WHERE Id = @Id;";
                nameCmd.Parameters.AddWithValue("@Id", id.ToString());
                var nameResult = await nameCmd.ExecuteScalarAsync(cancellationToken);
                if (nameResult != null && nameResult != DBNull.Value)
                {
                    companyName = nameResult.ToString();
                }
            }

            // Atomically check if transactions exist inside this exact transaction/connection
            await using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.Transaction = (SqliteTransaction)transaction;
                checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Vouchers';";
                var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
                if (tableCount > 0)
                {
                    await using var txCmd = connection.CreateCommand();
                    txCmd.Transaction = (SqliteTransaction)transaction;
                    txCmd.CommandText = @"
                        SELECT COUNT(1) FROM Vouchers v
                        INNER JOIN FinancialYears fy ON v.FinancialYearId = fy.Id
                        WHERE fy.CompanyId = @CompanyId;
                    ";
                    txCmd.Parameters.AddWithValue("@CompanyId", id.ToString());
                    var txCount = Convert.ToInt64(await txCmd.ExecuteScalarAsync(cancellationToken));
                    if (txCount > 0)
                    {
                        var nameDisplay = companyName ?? id.ToString();
                        throw new DomainException($"Cannot delete company '{nameDisplay}' because it has one or more recorded financial transactions / vouchers.");
                    }
                }
            }

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = "DELETE FROM Companies WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Company ReadCompany(DbDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var name = reader.GetString(1);
        var printName = reader.GetString(2);
        var shortName = reader.IsDBNull(3) ? null : reader.GetString(3);
        var country = reader.GetString(4);
        var stateName = reader.GetString(5);
        var stateCode = reader.GetString(6);
        var fyStart = DateOnly.Parse(reader.GetString(7));
        var booksStart = DateOnly.Parse(reader.GetString(8));
        var currSym = reader.GetString(9);
        var currFormal = reader.GetString(10);
        var gstin = reader.IsDBNull(11) ? null : reader.GetString(11);
        var regType = (GSTRegistrationType)reader.GetInt32(12);
        var isSec = reader.GetInt32(13) == 1;

        var company = new Company(
            id, name, printName, shortName, country, stateName, stateCode,
            fyStart, booksStart, currSym, currFormal, gstin, regType, isSec)
        {
            CreatedAt = DateTime.Parse(reader.GetString(14)),
            CreatedBy = reader.GetString(15),
            ModifiedAt = reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16)),
            ModifiedBy = reader.IsDBNull(17) ? null : reader.GetString(17)
        };

        return company;
    }

    private static async Task LoadFinancialYearsAsync(SqliteConnection connection, Company company, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CompanyId, YearName, StartDate, EndDate, IsActive, IsLocked, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM FinancialYears WHERE CompanyId = @CompanyId ORDER BY StartDate ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", company.Id.ToString());

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

            company.AddFinancialYear(fy);
        }
    }

    private static void AddCompanyParameters(SqliteCommand cmd, Company company)
    {
        cmd.Parameters.AddWithValue("@Id", company.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyName", company.CompanyName);
        cmd.Parameters.AddWithValue("@PrintName", company.PrintName);
        cmd.Parameters.AddWithValue("@ShortName", (object?)company.ShortName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Country", company.Country);
        cmd.Parameters.AddWithValue("@StateName", company.StateName);
        cmd.Parameters.AddWithValue("@StateCode", company.StateCode);
        cmd.Parameters.AddWithValue("@FinancialYearBeginning", company.FinancialYearBeginning.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@BooksBeginningFrom", company.BooksBeginningFrom.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@CurrencySymbol", company.CurrencySymbol);
        cmd.Parameters.AddWithValue("@CurrencyFormalName", company.CurrencyFormalName);
        cmd.Parameters.AddWithValue("@GSTIN", (object?)company.GSTIN ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GSTRegistrationType", (int)company.GSTRegistrationType);
        cmd.Parameters.AddWithValue("@IsSecurityEnabled", company.IsSecurityEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", company.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedBy", company.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", company.ModifiedAt.HasValue ? company.ModifiedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)company.ModifiedBy ?? DBNull.Value);
    }
}

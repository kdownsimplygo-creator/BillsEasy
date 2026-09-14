# BILLSEASY — CLAUDE FORENSIC REVIEW: INFRASTRUCTURE LAYER SOURCE
**Target Directory**: W:\BillsEasy\src\BillsEasy.Infrastructure
**Date**: 2026-09-13

---
## FILE: src\BillsEasy.Infrastructure\BillsEasy.Infrastructure.csproj
`xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\BillsEasy.Application\BillsEasy.Application.csproj" />
    <ProjectReference Include="..\BillsEasy.Domain\BillsEasy.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.8" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

`

---
## FILE: src\BillsEasy.Infrastructure\Persistence\DatabaseInitializer.cs
`csharp
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;

namespace BillsEasy.Infrastructure.Persistence;

public class DatabaseInitializer
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Companies (
                Id TEXT PRIMARY KEY,
                CompanyName TEXT NOT NULL,
                PrintName TEXT NOT NULL,
                ShortName TEXT,
                Country TEXT NOT NULL DEFAULT 'India',
                StateName TEXT NOT NULL,
                StateCode TEXT NOT NULL,
                FinancialYearBeginning TEXT NOT NULL,
                BooksBeginningFrom TEXT NOT NULL,
                CurrencySymbol TEXT NOT NULL DEFAULT '₹',
                CurrencyFormalName TEXT NOT NULL DEFAULT 'INR',
                GSTIN TEXT,
                GSTRegistrationType INTEGER NOT NULL DEFAULT 1 CHECK (GSTRegistrationType BETWEEN 1 AND 6),
                IsSecurityEnabled INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Companies_Name ON Companies(CompanyName COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS FinancialYears (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                YearName TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 0,
                IsLocked INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_FinancialYears_CompanyId ON FinancialYears(CompanyId);
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_FinancialYears_Company_OneActive ON FinancialYears(CompanyId) WHERE IsActive = 1;

            CREATE TABLE IF NOT EXISTS AccountGroups (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                Name TEXT NOT NULL,
                ParentGroupId TEXT,
                Nature INTEGER NOT NULL CHECK (Nature BETWEEN 1 AND 5),
                IsPrimary INTEGER NOT NULL DEFAULT 0,
                IsDefaultSystemGroup INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
                FOREIGN KEY (ParentGroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
            );
            
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Parent_Name
                ON AccountGroups(CompanyId, ParentGroupId, Name COLLATE NOCASE)
                WHERE ParentGroupId IS NOT NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Root_Name
                ON AccountGroups(CompanyId, Name COLLATE NOCASE)
                WHERE ParentGroupId IS NULL;

            CREATE INDEX IF NOT EXISTS IX_AccountGroups_ParentGroupId ON AccountGroups(ParentGroupId);
            CREATE INDEX IF NOT EXISTS IX_AccountGroups_CompanyId ON AccountGroups(CompanyId);

            CREATE TABLE IF NOT EXISTS Ledgers (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                GroupId TEXT NOT NULL,
                Name TEXT NOT NULL,
                PrintName TEXT NOT NULL,
                Alias TEXT,
                OpeningBalance INTEGER NOT NULL DEFAULT 0,
                OpeningBalanceType INTEGER NOT NULL DEFAULT 1 CHECK (OpeningBalanceType IN (1, 2)),
                BillWiseEnabled INTEGER NOT NULL DEFAULT 0,
                CreditPeriodDays INTEGER,
                CreditLimit INTEGER,
                AddressLine1 TEXT,
                AddressLine2 TEXT,
                City TEXT,
                StateName TEXT,
                StateCode TEXT,
                PinCode TEXT,
                Country TEXT NOT NULL DEFAULT 'India',
                GSTIN TEXT,
                GSTRegistrationType INTEGER NOT NULL DEFAULT 1 CHECK (GSTRegistrationType BETWEEN 1 AND 6),
                PAN TEXT,
                AadhaarNumber TEXT,
                ContactPerson TEXT,
                MobileNumber TEXT,
                WhatsAppNumber TEXT,
                Email TEXT,
                BankName TEXT,
                BankAccountNumber TEXT,
                BankIFSC TEXT,
                BankBranch TEXT,
                IsDefaultSystemLedger INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
                FOREIGN KEY (GroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Ledgers_Company_Name ON Ledgers(CompanyId, Name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Ledgers_GroupId ON Ledgers(GroupId);
            CREATE INDEX IF NOT EXISTS IX_Ledgers_CompanyId ON Ledgers(CompanyId);
        ";

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
`

---
## FILE: src\BillsEasy.Infrastructure\Persistence\Repositories\AccountGroupRepository.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class AccountGroupRepository : IAccountGroupRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public AccountGroupRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AccountGroup?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<AccountGroup?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<AccountGroup>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId
            ORDER BY Nature ASC, Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<AccountGroup>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<AccountGroup>> GetByParentIdAsync(Guid companyId, Guid parentId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId AND ParentGroupId = @ParentGroupId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@ParentGroupId", parentId.ToString());

        var list = new List<AccountGroup>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? parentGroupId = null, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        var sql = @"
            SELECT COUNT(1) FROM AccountGroups
            WHERE CompanyId = @CompanyId 
              AND LOWER(Name) = LOWER(@Name)
        ";

        if (parentGroupId.HasValue)
        {
            sql += " AND ParentGroupId = @ParentGroupId";
            cmd.Parameters.AddWithValue("@ParentGroupId", parentGroupId.Value.ToString());
        }
        else
        {
            sql += " AND ParentGroupId IS NULL";
        }

        if (excludeId.HasValue)
        {
            sql += " AND Id <> @ExcludeId";
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value.ToString());
        }

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasChildrenAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT COUNT(1) FROM AccountGroups
            WHERE CompanyId = @CompanyId AND ParentGroupId = @GroupId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasLedgersAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        // Check if Ledgers table exists first
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Ledgers';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM Ledgers
            WHERE CompanyId = @CompanyId AND GroupId = @GroupId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(AccountGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO AccountGroups (
                Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            ) VALUES (
                @Id, @CompanyId, @Name, @ParentGroupId, @Nature, @IsPrimary, @IsDefaultSystemGroup, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
            );
        ";

        AddParameters(cmd, group);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<AccountGroup> groups, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        foreach (var group in groups)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO AccountGroups (
                    Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyId, @Name, @ParentGroupId, @Nature, @IsPrimary, @IsDefaultSystemGroup, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";

            AddParameters(cmd, group);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(AccountGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE AccountGroups SET
                Name = @Name,
                ParentGroupId = @ParentGroupId,
                Nature = @Nature,
                IsPrimary = @IsPrimary,
                IsDefaultSystemGroup = @IsDefaultSystemGroup,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";

        AddParameters(cmd, group);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM AccountGroups WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, AccountGroup group)
    {
        cmd.Parameters.AddWithValue("@Id", group.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", group.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@Name", group.Name);
        cmd.Parameters.AddWithValue("@ParentGroupId", group.ParentGroupId.HasValue ? (object)group.ParentGroupId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@Nature", (int)group.Nature);
        cmd.Parameters.AddWithValue("@IsPrimary", group.IsPrimary ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsDefaultSystemGroup", group.IsDefaultSystemGroup ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@CreatedBy", group.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", group.ModifiedAt.HasValue ? (object)group.ModifiedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", group.ModifiedBy ?? (object)DBNull.Value);
    }

    private static AccountGroup MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var name = reader.GetString(2);
        Guid? parentGroupId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3));
        var nature = (GroupNature)reader.GetInt32(4);
        var isPrimary = reader.GetInt32(5) == 1;
        var isDefaultSystemGroup = reader.GetInt32(6) == 1;

        return new AccountGroup(id, companyId, name, nature, parentGroupId, isPrimary, isDefaultSystemGroup)
        {
            CreatedAt = DateTime.Parse(reader.GetString(7)),
            CreatedBy = reader.GetString(8),
            ModifiedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
            ModifiedBy = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }
}
`

---
## FILE: src\BillsEasy.Infrastructure\Persistence\Repositories\CompanyRepository.cs
`csharp
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
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

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Companies WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
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

`

---
## FILE: src\BillsEasy.Infrastructure\Persistence\Repositories\FinancialYearRepository.cs
`csharp
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

`

---
## FILE: src\BillsEasy.Infrastructure\Persistence\Repositories\LedgerRepository.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class LedgerRepository : ILedgerRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public LedgerRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Ledger?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<Ledger?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<Ledger>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<Ledger>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId AND GroupId = @GroupId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var list = new List<Ledger>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        var sql = @"
            SELECT COUNT(1) FROM Ledgers
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name)
        ";

        if (excludeId.HasValue)
        {
            sql += " AND Id <> @ExcludeId";
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value.ToString());
        }

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasTransactionsAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='VoucherLines';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM VoucherLines vl
            INNER JOIN Ledgers l ON vl.LedgerId = l.Id
            WHERE l.CompanyId = @CompanyId AND vl.LedgerId = @LedgerId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasBillReferencesAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='BillReferences';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM BillReferences
            WHERE CompanyId = @CompanyId AND LedgerId = @LedgerId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(Ledger ledger, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO Ledgers (
                Id, CompanyId, GroupId, Name, PrintName, Alias,
                OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                ContactPerson, MobileNumber, WhatsAppNumber, Email,
                BankName, BankAccountNumber, BankIFSC, BankBranch,
                IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            ) VALUES (
                @Id, @CompanyId, @GroupId, @Name, @PrintName, @Alias,
                @OpeningBalance, @OpeningBalanceType, @BillWiseEnabled, @CreditPeriodDays, @CreditLimit,
                @AddressLine1, @AddressLine2, @City, @StateName, @StateCode, @PinCode, @Country,
                @GSTIN, @GSTRegistrationType, @PAN, @AadhaarNumber,
                @ContactPerson, @MobileNumber, @WhatsAppNumber, @Email,
                @BankName, @BankAccountNumber, @BankIFSC, @BankBranch,
                @IsDefaultSystemLedger, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
            );
        ";

        AddParameters(cmd, ledger);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Ledger> ledgers, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        foreach (var ledger in ledgers)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Ledgers (
                    Id, CompanyId, GroupId, Name, PrintName, Alias,
                    OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                    AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                    GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                    ContactPerson, MobileNumber, WhatsAppNumber, Email,
                    BankName, BankAccountNumber, BankIFSC, BankBranch,
                    IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyId, @GroupId, @Name, @PrintName, @Alias,
                    @OpeningBalance, @OpeningBalanceType, @BillWiseEnabled, @CreditPeriodDays, @CreditLimit,
                    @AddressLine1, @AddressLine2, @City, @StateName, @StateCode, @PinCode, @Country,
                    @GSTIN, @GSTRegistrationType, @PAN, @AadhaarNumber,
                    @ContactPerson, @MobileNumber, @WhatsAppNumber, @Email,
                    @BankName, @BankAccountNumber, @BankIFSC, @BankBranch,
                    @IsDefaultSystemLedger, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";

            AddParameters(cmd, ledger);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(Ledger ledger, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE Ledgers SET
                GroupId = @GroupId,
                Name = @Name,
                PrintName = @PrintName,
                Alias = @Alias,
                OpeningBalance = @OpeningBalance,
                OpeningBalanceType = @OpeningBalanceType,
                BillWiseEnabled = @BillWiseEnabled,
                CreditPeriodDays = @CreditPeriodDays,
                CreditLimit = @CreditLimit,
                AddressLine1 = @AddressLine1,
                AddressLine2 = @AddressLine2,
                City = @City,
                StateName = @StateName,
                StateCode = @StateCode,
                PinCode = @PinCode,
                Country = @Country,
                GSTIN = @GSTIN,
                GSTRegistrationType = @GSTRegistrationType,
                PAN = @PAN,
                AadhaarNumber = @AadhaarNumber,
                ContactPerson = @ContactPerson,
                MobileNumber = @MobileNumber,
                WhatsAppNumber = @WhatsAppNumber,
                Email = @Email,
                BankName = @BankName,
                BankAccountNumber = @BankAccountNumber,
                BankIFSC = @BankIFSC,
                BankBranch = @BankBranch,
                IsDefaultSystemLedger = @IsDefaultSystemLedger,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";

        AddParameters(cmd, ledger);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM Ledgers WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, Ledger ledger)
    {
        cmd.Parameters.AddWithValue("@Id", ledger.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", ledger.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", ledger.GroupId.ToString());
        cmd.Parameters.AddWithValue("@Name", ledger.Name);
        cmd.Parameters.AddWithValue("@PrintName", ledger.PrintName);
        cmd.Parameters.AddWithValue("@Alias", (object?)ledger.Alias ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OpeningBalance", ledger.OpeningBalance.ToMinorUnits());
        cmd.Parameters.AddWithValue("@OpeningBalanceType", (int)ledger.OpeningBalanceType);
        cmd.Parameters.AddWithValue("@BillWiseEnabled", ledger.BillWiseEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreditPeriodDays", (object?)ledger.CreditPeriodDays ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreditLimit", ledger.CreditLimit.HasValue ? (object)ledger.CreditLimit.Value.ToMinorUnits() : DBNull.Value);
        cmd.Parameters.AddWithValue("@AddressLine1", (object?)ledger.AddressLine1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AddressLine2", (object?)ledger.AddressLine2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City", (object?)ledger.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateName", (object?)ledger.StateName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateCode", (object?)ledger.StateCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PinCode", (object?)ledger.PinCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Country", ledger.Country);
        cmd.Parameters.AddWithValue("@GSTIN", (object?)ledger.GSTIN ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GSTRegistrationType", (int)ledger.GSTRegistrationType);
        cmd.Parameters.AddWithValue("@PAN", (object?)ledger.PAN ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AadhaarNumber", (object?)ledger.AadhaarNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContactPerson", (object?)ledger.ContactPerson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MobileNumber", (object?)ledger.MobileNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@WhatsAppNumber", (object?)ledger.WhatsAppNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)ledger.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankName", (object?)ledger.BankName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankAccountNumber", (object?)ledger.BankAccountNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankIFSC", (object?)ledger.BankIFSC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankBranch", (object?)ledger.BankBranch ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDefaultSystemLedger", ledger.IsDefaultSystemLedger ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", ledger.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@CreatedBy", ledger.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", ledger.ModifiedAt.HasValue ? (object)ledger.ModifiedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)ledger.ModifiedBy ?? DBNull.Value);
    }

    private static Ledger MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var groupId = Guid.Parse(reader.GetString(2));
        var name = reader.GetString(3);
        var printName = reader.GetString(4);
        var alias = reader.IsDBNull(5) ? null : reader.GetString(5);
        var openingBalance = Money.FromMinorUnits(reader.GetInt64(6));
        var openingBalanceType = (DebitCreditType)reader.GetInt32(7);
        var billWiseEnabled = reader.GetInt32(8) == 1;
        int? creditPeriodDays = reader.IsDBNull(9) ? null : reader.GetInt32(9);
        Money? creditLimit = reader.IsDBNull(10) ? null : Money.FromMinorUnits(reader.GetInt64(10));
        var addressLine1 = reader.IsDBNull(11) ? null : reader.GetString(11);
        var addressLine2 = reader.IsDBNull(12) ? null : reader.GetString(12);
        var city = reader.IsDBNull(13) ? null : reader.GetString(13);
        var stateName = reader.IsDBNull(14) ? null : reader.GetString(14);
        var stateCode = reader.IsDBNull(15) ? null : reader.GetString(15);
        var pinCode = reader.IsDBNull(16) ? null : reader.GetString(16);
        var country = reader.GetString(17);
        var gstin = reader.IsDBNull(18) ? null : reader.GetString(18);
        var gstRegistrationType = (GSTRegistrationType)reader.GetInt32(19);
        var pan = reader.IsDBNull(20) ? null : reader.GetString(20);
        var aadhaarNumber = reader.IsDBNull(21) ? null : reader.GetString(21);
        var contactPerson = reader.IsDBNull(22) ? null : reader.GetString(22);
        var mobileNumber = reader.IsDBNull(23) ? null : reader.GetString(23);
        var whatsAppNumber = reader.IsDBNull(24) ? null : reader.GetString(24);
        var email = reader.IsDBNull(25) ? null : reader.GetString(25);
        var bankName = reader.IsDBNull(26) ? null : reader.GetString(26);
        var bankAccountNumber = reader.IsDBNull(27) ? null : reader.GetString(27);
        var bankIfsc = reader.IsDBNull(28) ? null : reader.GetString(28);
        var bankBranch = reader.IsDBNull(29) ? null : reader.GetString(29);
        var isDefaultSystemLedger = reader.GetInt32(30) == 1;

        return new Ledger(
            id, companyId, groupId, name, printName, alias,
            openingBalance, openingBalanceType, billWiseEnabled, creditPeriodDays, creditLimit,
            addressLine1, addressLine2, city, stateName, stateCode, pinCode, country,
            gstin, gstRegistrationType, pan, aadhaarNumber,
            contactPerson, mobileNumber, whatsAppNumber, email,
            bankName, bankAccountNumber, bankIfsc, bankBranch,
            isDefaultSystemLedger)
        {
            CreatedAt = DateTime.Parse(reader.GetString(31)),
            CreatedBy = reader.GetString(32),
            ModifiedAt = reader.IsDBNull(33) ? null : DateTime.Parse(reader.GetString(33)),
            ModifiedBy = reader.IsDBNull(34) ? null : reader.GetString(34)
        };
    }
}
`

---
## FILE: src\BillsEasy.Infrastructure\Persistence\SqliteConnectionFactory.cs
`csharp
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence;

/// <summary>
/// Thread-safe factory for SQLite connections configured with WAL mode and foreign key enforcement.
/// </summary>
public class SqliteConnectionFactory : ISqliteDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public DbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ConfigurePragmas(connection);
        return connection;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await ConfigurePragmasAsync(connection, cancellationToken);
        return connection;
    }

    private static void ConfigurePragmas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        if (!connection.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        }
        else
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
        }
        command.ExecuteNonQuery();
    }

    private static async Task ConfigurePragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        if (!connection.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        }
        else
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

`

---
## FILE: src\BillsEasy.Infrastructure\Services\CompanyContextService.cs
`csharp
using System;
using System.Linq;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Infrastructure.Services;

public class CompanyContextService : ICompanyContextService
{
    public Company? ActiveCompany { get; private set; }
    public FinancialYear? ActiveFinancialYear { get; private set; }

    public event EventHandler? ActiveCompanyChanged;

    public void SetActiveContext(Company company, FinancialYear? financialYear = null)
    {
        ActiveCompany = company ?? throw new ArgumentNullException(nameof(company));
        ActiveFinancialYear = financialYear ?? company.FinancialYears.FirstOrDefault(f => f.IsActive) ?? company.FinancialYears.FirstOrDefault();
        ActiveCompanyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActiveFinancialYear(FinancialYear financialYear)
    {
        if (ActiveCompany == null)
            throw new InvalidOperationException("Cannot set active financial year when no company is open.");

        if (financialYear.CompanyId != ActiveCompany.Id)
            throw new InvalidOperationException("Financial year does not belong to active company.");

        ActiveFinancialYear = financialYear;
        ActiveCompany.SetActiveFinancialYear(financialYear.Id);
        ActiveCompanyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CloseActiveCompany()
    {
        ActiveCompany = null;
        ActiveFinancialYear = null;
        ActiveCompanyChanged?.Invoke(this, EventArgs.Empty);
    }
}

`

---
## FILE: src\BillsEasy.Infrastructure\Services\DateTimeProvider.cs
`csharp
using System;
using BillsEasy.Application.Common.Interfaces;

namespace BillsEasy.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}

public class StandaloneCurrentUserService : ICurrentUserService
{
    public string UserId => Environment.UserName.ToLowerInvariant();
    public string UserName => Environment.UserName;
    public string WorkstationName => Environment.MachineName;
}

`


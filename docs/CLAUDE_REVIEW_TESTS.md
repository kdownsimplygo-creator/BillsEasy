# BILLSEASY — CLAUDE FORENSIC REVIEW: COMPLETE TEST SUITE SOURCE
**Target Directory**: W:\BillsEasy\tests
**Date**: 2026-09-13

---
## FILE: tests\BillsEasy.IntegrationTests\BillsEasy.IntegrationTests.csproj
`xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.8" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BillsEasy.Infrastructure\BillsEasy.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\BillsEasy.Application\BillsEasy.Application.csproj" />
    <ProjectReference Include="..\..\src\BillsEasy.Domain\BillsEasy.Domain.csproj" />
  </ItemGroup>

</Project>

`

---
## FILE: tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\AccountGroupRepositoryTests.cs
`csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class AccountGroupRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly AccountGroupRepository _accountGroupRepository;

    public AccountGroupRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_GrpTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _accountGroupRepository = new AccountGroupRepository(_connectionFactory);
    }

    private async Task<Company> CreateTestCompanyAsync(string name = "Test Company")
    {
        var company = Company.Create(
            companyName: name,
            printName: name,
            shortName: "TC",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";

        await _companyRepository.AddAsync(company);
        return company;
    }

    [Fact]
    public async Task AddAndGetById_PersistsAccountGroupSuccessfully()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var group = AccountGroup.Create(
            company.Id,
            "Sundry Debtors",
            GroupNature.Asset,
            parentGroupId: null,
            isPrimary: true,
            isDefaultSystemGroup: true);

        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";

        await _accountGroupRepository.AddAsync(group);

        var retrieved = await _accountGroupRepository.GetByIdAsync(company.Id, group.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Sundry Debtors");
        retrieved.Nature.Should().Be(GroupNature.Asset);
        retrieved.IsPrimary.Should().BeTrue();
        retrieved.IsDefaultSystemGroup.Should().BeTrue();
        retrieved.CreatedBy.Should().Be("tester");
    }

    [Fact]
    public async Task AddRange_PersistsMultipleGroupsInHierarchy()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var primaryAsset = AccountGroup.Create(company.Id, "Assets", GroupNature.Asset, isPrimary: true, isDefaultSystemGroup: true);
        var currAsset = AccountGroup.Create(company.Id, "Current Assets", GroupNature.Asset, parentGroupId: primaryAsset.Id, isDefaultSystemGroup: true);
        var debtors = AccountGroup.Create(company.Id, "Sundry Debtors", GroupNature.Asset, parentGroupId: currAsset.Id, isDefaultSystemGroup: true);

        var groups = new List<AccountGroup> { primaryAsset, currAsset, debtors };
        foreach (var g in groups)
        {
            g.CreatedAt = DateTime.UtcNow;
            g.CreatedBy = "tester";
        }

        await _accountGroupRepository.AddRangeAsync(groups);

        var all = await _accountGroupRepository.GetAllAsync(company.Id);
        all.Should().HaveCount(3);

        var childrenOfCurr = await _accountGroupRepository.GetByParentIdAsync(company.Id, currAsset.Id);
        childrenOfCurr.Should().ContainSingle(g => g.Name == "Sundry Debtors");

        var hasChildren = await _accountGroupRepository.HasChildrenAsync(company.Id, currAsset.Id);
        hasChildren.Should().BeTrue();

        var debtorsHasChildren = await _accountGroupRepository.HasChildrenAsync(company.Id, debtors.Id);
        debtorsHasChildren.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByName_EnforcesCompanyIsolation()
    {
        await _initializer.InitializeAsync();
        var companyA = await CreateTestCompanyAsync("Company Alpha");
        var companyB = await CreateTestCompanyAsync("Company Beta");

        var groupA = AccountGroup.Create(companyA.Id, "Custom Group", GroupNature.Expense);
        groupA.CreatedAt = DateTime.UtcNow;
        groupA.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(groupA);

        var existsInA = await _accountGroupRepository.ExistsByNameAsync(companyA.Id, "Custom Group", null);
        var existsInB = await _accountGroupRepository.ExistsByNameAsync(companyB.Id, "Custom Group", null);

        existsInA.Should().BeTrue();
        existsInB.Should().BeFalse(); // Isolated by company
    }

    [Fact]
    public async Task DuplicateSiblingNameInSameCompany_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var parent = AccountGroup.Create(company.Id, "Parent Group", GroupNature.Asset, isPrimary: true);
        parent.CreatedAt = DateTime.UtcNow;
        parent.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(parent);

        var child1 = AccountGroup.Create(company.Id, "Child Group", GroupNature.Asset, parentGroupId: parent.Id);
        child1.CreatedAt = DateTime.UtcNow;
        child1.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(child1);

        var child2 = AccountGroup.Create(company.Id, "Child Group", GroupNature.Asset, parentGroupId: parent.Id);
        child2.CreatedAt = DateTime.UtcNow;
        child2.CreatedBy = "tester";

        var act = async () => await _accountGroupRepository.AddAsync(child2);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*UNIQUE constraint failed*");
    }

    [Fact]
    public async Task SameNameUnderDifferentParents_PersistsSuccessfully()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var parent1 = AccountGroup.Create(company.Id, "Parent One", GroupNature.Liability, isPrimary: true);
        var parent2 = AccountGroup.Create(company.Id, "Parent Two", GroupNature.Expense, isPrimary: true);
        parent1.CreatedAt = DateTime.UtcNow;
        parent1.CreatedBy = "tester";
        parent2.CreatedAt = DateTime.UtcNow;
        parent2.CreatedBy = "tester";
        await _accountGroupRepository.AddRangeAsync(new[] { parent1, parent2 });

        var child1 = AccountGroup.Create(company.Id, "Outstanding Expenses", GroupNature.Liability, parentGroupId: parent1.Id);
        var child2 = AccountGroup.Create(company.Id, "Outstanding Expenses", GroupNature.Expense, parentGroupId: parent2.Id);
        child1.CreatedAt = DateTime.UtcNow;
        child1.CreatedBy = "tester";
        child2.CreatedAt = DateTime.UtcNow;
        child2.CreatedBy = "tester";

        await _accountGroupRepository.AddAsync(child1);
        var act = async () => await _accountGroupRepository.AddAsync(child2);

        await act.Should().NotThrowAsync();

        var all = await _accountGroupRepository.GetAllAsync(company.Id);
        all.Count(g => g.Name == "Outstanding Expenses").Should().Be(2);
    }

    [Fact]
    public async Task DeleteCompany_CascadesToAccountGroups()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var group = AccountGroup.Create(company.Id, "Group To Cascade", GroupNature.Capital);
        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(group);

        // Delete Company
        await _companyRepository.DeleteAsync(company.Id);

        // Account groups for company should be gone
        var remaining = await _accountGroupRepository.GetAllAsync(company.Id);
        remaining.Should().BeEmpty();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Best effort cleanup in temp directory
        }
    }
}
`

---
## FILE: tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\CompanyRepositoryTests.cs
`csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class CompanyRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;

    public CompanyRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_CompTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
    }

    [Fact]
    public async Task AddAndGetById_PersistsCompanyAndFinancialYearsSuccessfully()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Shree Ganesh Enterprise",
            printName: "Shree Ganesh Enterprise",
            shortName: "SGE",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1),
            gstin: "24AAACS1234A1Z5",
            gstRegistrationType: GSTRegistrationType.Regular);

        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "test_user";

        await _companyRepository.AddAsync(company);

        var retrieved = await _companyRepository.GetByIdAsync(company.Id);

        retrieved.Should().NotBeNull();
        retrieved!.CompanyName.Should().Be("Shree Ganesh Enterprise");
        retrieved.StateName.Should().Be("Gujarat");
        retrieved.StateCode.Should().Be("24");
        retrieved.GSTIN.Should().Be("24AAACS1234A1Z5");
        retrieved.CreatedBy.Should().Be("test_user");

        retrieved.FinancialYears.Should().HaveCount(1);
        var fy = retrieved.FinancialYears.First();
        fy.StartDate.Should().Be(new DateOnly(2024, 4, 1));
        fy.EndDate.Should().Be(new DateOnly(2025, 3, 31));
        fy.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByName_DetectsDuplicateCompanyNamesCaseInsensitive()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Krishna Traders",
            printName: "Krishna Traders",
            shortName: "KT",
            stateName: "Maharashtra",
            stateCode: "27",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        await _companyRepository.AddAsync(company);

        var exists1 = await _companyRepository.ExistsByNameAsync("krishna traders");
        var exists2 = await _companyRepository.ExistsByNameAsync("KRISHNA TRADERS");
        var exists3 = await _companyRepository.ExistsByNameAsync("NonExistent");
        var existsExcludingSelf = await _companyRepository.ExistsByNameAsync("Krishna Traders", company.Id);

        exists1.Should().BeTrue();
        exists2.Should().BeTrue();
        exists3.Should().BeFalse();
        existsExcludingSelf.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ModifiesPropertiesAndPersistsCorrectly()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Original Corp",
            printName: "Original Corp",
            shortName: "OC",
            stateName: "Delhi",
            stateCode: "07",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        await _companyRepository.AddAsync(company);

        company.Update("Updated Corp", "Updated Corp Pvt Ltd", "UC", "Delhi", "07", "07AAACU1234A1Z5", GSTRegistrationType.Composition, true);
        company.ModifiedAt = DateTime.UtcNow;
        company.ModifiedBy = "updater_user";

        await _companyRepository.UpdateAsync(company);

        var updated = await _companyRepository.GetByIdAsync(company.Id);

        updated.Should().NotBeNull();
        updated!.CompanyName.Should().Be("Updated Corp");
        updated.PrintName.Should().Be("Updated Corp Pvt Ltd");
        updated.GSTIN.Should().Be("07AAACU1234A1Z5");
        updated.GSTRegistrationType.Should().Be(GSTRegistrationType.Composition);
        updated.IsSecurityEnabled.Should().BeTrue();
        updated.ModifiedBy.Should().Be("updater_user");
    }

    [Fact]
    public async Task Delete_RemovesCompanyAndCascadesFinancialYears()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Delete Target",
            printName: "Delete Target",
            shortName: "DT",
            stateName: "Rajasthan",
            stateCode: "08",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        await _companyRepository.AddAsync(company);

        await _companyRepository.DeleteAsync(company.Id);

        var deleted = await _companyRepository.GetByIdAsync(company.Id);
        deleted.Should().BeNull();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            var wal = _testDbPath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _testDbPath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }
}

`

---
## FILE: tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\DatabaseConstraintIntegrationTests.cs
`csharp
using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class DatabaseConstraintIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly LedgerRepository _ledgerRepository;

    public DatabaseConstraintIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_DbConstraints_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
    }

    [Fact]
    public async Task Companies_CaseInsensitiveNameUniqueness_DatabaseRejectsDuplicate()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        // 1. Insert "ABC Traders"
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@Id, 'ABC Traders', 'ABC Traders', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Insert "abc traders" (case-insensitive collision)
        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@Id, 'abc traders', 'abc traders', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // 19 = SQLITE_CONSTRAINT (UNIQUE constraint failed: Companies.CompanyName)
    }

    [Fact]
    public async Task FinancialYears_MultipleActiveYears_DatabaseRejectsSecondActiveYear()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        var companyId = Guid.NewGuid().ToString();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Test Co', 'Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert first active FY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO FinancialYears (Id, CompanyId, YearName, StartDate, EndDate, IsActive, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'FY 2026-27', '2026-04-01', '2027-03-31', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert second active FY for the same company outside the transactional service path
        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FinancialYears (Id, CompanyId, YearName, StartDate, EndDate, IsActive, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'FY 2027-28', '2027-04-01', '2028-03-31', 1, '2027-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // UNIQUE constraint failed on partial index UQ_FinancialYears_Company_OneActive
    }

    [Fact]
    public async Task AccountGroups_NatureEnum_CheckConstraint_RejectsOutOfRangeValue()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        var companyId = Guid.NewGuid().ToString();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Group Test Co', 'Group Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        }

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'Invalid Group', 99, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: Nature BETWEEN 1 AND 5
    }

    [Fact]
    public async Task Ledgers_OpeningBalanceTypeEnum_CheckConstraint_RejectsOutOfRangeValue()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        var companyId = Guid.NewGuid().ToString();
        var groupId = Guid.NewGuid().ToString();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Ledger Test Co', 'Ledger Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');

                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@GroupId, @CompanyId, 'Assets', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            cmd.Parameters.AddWithValue("@GroupId", groupId);
            await cmd.ExecuteNonQueryAsync();
        }

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Ledgers (Id, CompanyId, GroupId, Name, PrintName, OpeningBalance, OpeningBalanceType, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, @GroupId, 'Invalid Type Ledger', 'Invalid Type Ledger', 1000, 3, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            cmd.Parameters.AddWithValue("@GroupId", groupId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: OpeningBalanceType IN (1, 2)
    }

    [Fact]
    public async Task Companies_GSTRegistrationTypeEnum_CheckConstraint_RejectsOutOfRangeValue()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, GSTRegistrationType, CreatedAt, CreatedBy)
                VALUES (@Id, 'Invalid GST Co', 'Invalid GST Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', 0, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: GSTRegistrationType BETWEEN 1 AND 6
    }

    [Fact]
    public async Task Ledgers_MonetaryRoundTripPrecision_StoresAndReadsExactPaise()
    {
        await _initializer.InitializeAsync();
        var companyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Money Co', 'Money Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');

                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@GroupId, @CompanyId, 'Current Assets', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        var ledger = Ledger.Create(
            companyId,
            groupId,
            "Precision Test Ledger",
            openingBalance: 1234.56m,
            openingBalanceType: DebitCreditType.Debit,
            creditLimit: 98765.43m);

        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "admin";

        await _ledgerRepository.AddAsync(ledger);

        // Verify underlying storage is exact INTEGER paise
        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT OpeningBalance, CreditLimit FROM Ledgers WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", ledger.Id.ToString());
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            var rawOpBal = reader.GetInt64(0);
            var rawCreditLimit = reader.GetInt64(1);

            rawOpBal.Should().Be(123456L, "Opening balance ₹1234.56 must be stored as 123456 paise");
            rawCreditLimit.Should().Be(9876543L, "Credit limit ₹98765.43 must be stored as 9876543 paise");
        }

        // Verify retrieval via repository reproduces exact decimal
        var retrieved = await _ledgerRepository.GetByIdAsync(companyId, ledger.Id);
        retrieved.Should().NotBeNull();
        retrieved!.OpeningBalance.Should().Be(new Money(1234.56m));
        retrieved.CreditLimit.Should().Be(new Money(98765.43m));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch { }
    }
}

`

---
## FILE: tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\FinancialYearRepositoryTests.cs
`csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class FinancialYearRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly FinancialYearRepository _financialYearRepository;

    public FinancialYearRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_FyTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _financialYearRepository = new FinancialYearRepository(_connectionFactory);
    }

    [Fact]
    public async Task SetActive_SwitchesActiveFinancialYearAtomically()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            "FY Switch Test Co",
            "FY Switch Test Co",
            null,
            "Gujarat",
            "24",
            new DateOnly(2024, 4, 1),
            new DateOnly(2024, 4, 1));

        var fy1 = company.FinancialYears.First();

        var fy2 = FinancialYear.Create(
            company.Id,
            new DateOnly(2025, 4, 1),
            new DateOnly(2026, 3, 31),
            "2025-2026",
            isActive: false);

        await _companyRepository.AddAsync(company);
        await _financialYearRepository.AddAsync(fy2);

        // Switch active year to FY2
        await _financialYearRepository.SetActiveAsync(company.Id, fy2.Id);

        var activeFy = await _financialYearRepository.GetActiveFinancialYearAsync(company.Id);
        activeFy.Should().NotBeNull();
        activeFy!.Id.Should().Be(fy2.Id);
        activeFy.YearName.Should().Be("2025-2026");

        var allFys = await _financialYearRepository.GetByCompanyIdAsync(company.Id);
        allFys.Should().HaveCount(2);
        allFys.First(f => f.Id == fy1.Id).IsActive.Should().BeFalse();
        allFys.First(f => f.Id == fy2.Id).IsActive.Should().BeTrue();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            var wal = _testDbPath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _testDbPath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }
}

`

---
## FILE: tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\LedgerRepositoryTests.cs
`csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class LedgerRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly AccountGroupRepository _accountGroupRepository;
    private readonly LedgerRepository _ledgerRepository;

    public LedgerRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_LedgTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _accountGroupRepository = new AccountGroupRepository(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
    }

    private async Task<(Company Company, AccountGroup Group)> CreateCompanyAndGroupAsync(string compName = "Test Co", string grpName = "Sundry Debtors")
    {
        var company = Company.Create(
            compName, compName, null, "Gujarat", "24",
            new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));
        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";
        await _companyRepository.AddAsync(company);

        var group = AccountGroup.Create(company.Id, grpName, GroupNature.Asset, isPrimary: true);
        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(group);

        return (company, group);
    }

    [Fact]
    public async Task AddAndGetById_PersistsLedgerSuccessfully()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var ledger = Ledger.Create(
            company.Id,
            group.Id,
            "Rohit Sharma",
            printName: "Rohit Sharma Enterprises",
            alias: "ROHIT",
            openingBalance: 25000.75m,
            openingBalanceType: DebitCreditType.Debit,
            billWiseEnabled: true,
            creditPeriodDays: 45,
            creditLimit: 100000m,
            addressLine1: "101 Trade Center",
            city: "Surat",
            stateName: "Gujarat",
            stateCode: "24",
            pinCode: "395001",
            gstin: "24AAACS1234A1Z5",
            gstRegistrationType: GSTRegistrationType.Regular,
            pan: "AAACS1234A",
            contactPerson: "Rohit",
            mobileNumber: "9876543210",
            email: "rohit@trade.com",
            bankName: "HDFC Bank",
            bankAccountNumber: "50100012345678",
            bankIfsc: "HDFC0000123",
            bankBranch: "Surat Main");

        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "tester";

        await _ledgerRepository.AddAsync(ledger);

        var retrieved = await _ledgerRepository.GetByIdAsync(company.Id, ledger.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Rohit Sharma");
        retrieved.PrintName.Should().Be("Rohit Sharma Enterprises");
        retrieved.Alias.Should().Be("ROHIT");
        retrieved.OpeningBalance.Should().Be(new Money(25000.75m));
        retrieved.OpeningBalanceType.Should().Be(DebitCreditType.Debit);
        retrieved.BillWiseEnabled.Should().BeTrue();
        retrieved.CreditPeriodDays.Should().Be(45);
        retrieved.CreditLimit.Should().Be(new Money(100000m));
        retrieved.City.Should().Be("Surat");
        retrieved.GSTIN.Should().Be("24AAACS1234A1Z5");
        retrieved.BankName.Should().Be("HDFC Bank");
        retrieved.CreatedBy.Should().Be("tester");
    }

    [Fact]
    public async Task AddRange_PersistsMultipleLedgers()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var l1 = Ledger.Create(company.Id, group.Id, "Customer A", openingBalance: 1000m);
        var l2 = Ledger.Create(company.Id, group.Id, "Customer B", openingBalance: 2000m);
        l1.CreatedAt = DateTime.UtcNow;
        l1.CreatedBy = "tester";
        l2.CreatedAt = DateTime.UtcNow;
        l2.CreatedBy = "tester";

        await _ledgerRepository.AddRangeAsync(new[] { l1, l2 });

        var all = await _ledgerRepository.GetAllAsync(company.Id);
        all.Should().HaveCount(2);

        var groupLedgers = await _ledgerRepository.GetByGroupIdAsync(company.Id, group.Id);
        groupLedgers.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExistsByName_EnforcesCompanyIsolation()
    {
        await _initializer.InitializeAsync();
        var (compA, grpA) = await CreateCompanyAndGroupAsync("Company A", "Group A");
        var (compB, grpB) = await CreateCompanyAndGroupAsync("Company B", "Group B");

        var ledgerA = Ledger.Create(compA.Id, grpA.Id, "Standard Ledger");
        ledgerA.CreatedAt = DateTime.UtcNow;
        ledgerA.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(ledgerA);

        var existsInA = await _ledgerRepository.ExistsByNameAsync(compA.Id, "Standard Ledger");
        var existsInB = await _ledgerRepository.ExistsByNameAsync(compB.Id, "Standard Ledger");

        existsInA.Should().BeTrue();
        existsInB.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateNameInSameCompany_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var l1 = Ledger.Create(company.Id, group.Id, "Unique Ledger");
        l1.CreatedAt = DateTime.UtcNow;
        l1.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(l1);

        var l2 = Ledger.Create(company.Id, group.Id, "unique ledger"); // Case-insensitive collision
        l2.CreatedAt = DateTime.UtcNow;
        l2.CreatedBy = "tester";

        var act = async () => await _ledgerRepository.AddAsync(l2);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*UNIQUE constraint failed: Ledgers.CompanyId, Ledgers.Name*");
    }

    [Fact]
    public async Task DeleteAccountGroup_WhenReferencedByLedger_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var ledger = Ledger.Create(company.Id, group.Id, "Dependent Ledger");
        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(ledger);

        // Attempt to delete AccountGroup referenced by Ledger -> RESTRICT foreign key constraint triggers
        var act = async () => await _accountGroupRepository.DeleteAsync(company.Id, group.Id);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task DeleteCompany_CascadesToLedgers()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var ledger = Ledger.Create(company.Id, group.Id, "Ledger To Cascade");
        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(ledger);

        // Delete Company
        await _companyRepository.DeleteAsync(company.Id);

        var remaining = await _ledgerRepository.GetAllAsync(company.Id);
        remaining.Should().BeEmpty();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Best effort cleanup in temp directory
        }
    }
}
`

---
## FILE: tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\SqliteConnectionFactoryTests.cs
`csharp
using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class SqliteConnectionFactoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;

    public SqliteConnectionFactoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_Test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_testDbPath}";
    }

    [Fact]
    public void CreateConnection_OpensConnectionAndConfiguresWalAndForeignKeys()
    {
        var factory = new SqliteConnectionFactory(_connectionString);

        using (var connection = factory.CreateConnection())
        {
            connection.State.Should().Be(System.Data.ConnectionState.Open);

            // Verify Foreign Keys Pragma
            using (var fkCmd = connection.CreateCommand())
            {
                fkCmd.CommandText = "PRAGMA foreign_keys;";
                var fkResult = fkCmd.ExecuteScalar();
                Convert.ToInt64(fkResult).Should().Be(1L, "foreign_keys should be enabled");
            }

            // Verify WAL Mode Pragma
            using (var walCmd = connection.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA journal_mode;";
                var walResult = walCmd.ExecuteScalar();
                walResult?.ToString().Should().BeEquivalentTo("wal", "journal_mode should be WAL");
            }
        }
    }

    [Fact]
    public async Task CreateConnectionAsync_ExecutesSqlTableCreationInsertAndSelect()
    {
        var factory = new SqliteConnectionFactory(_connectionString);

        await using var connection = await factory.CreateConnectionAsync();

        // 1. Create table
        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = @"
                CREATE TABLE TestAccountingMasters (
                    Id TEXT PRIMARY KEY,
                    Code TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    OpeningBalance REAL NOT NULL
                );";
            await createCmd.ExecuteNonQueryAsync();
        }

        // 2. Insert row
        var masterId = Guid.NewGuid().ToString();
        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO TestAccountingMasters (Id, Code, Name, OpeningBalance)
                VALUES (@Id, @Code, @Name, @OpeningBalance);";

            var idParam = insertCmd.CreateParameter();
            idParam.ParameterName = "@Id";
            idParam.Value = masterId;
            insertCmd.Parameters.Add(idParam);

            var codeParam = insertCmd.CreateParameter();
            codeParam.ParameterName = "@Code";
            codeParam.Value = "SD-001";
            insertCmd.Parameters.Add(codeParam);

            var nameParam = insertCmd.CreateParameter();
            nameParam.ParameterName = "@Name";
            nameParam.Value = "Sundry Debtors North";
            insertCmd.Parameters.Add(nameParam);

            var balParam = insertCmd.CreateParameter();
            balParam.ParameterName = "@OpeningBalance";
            balParam.Value = 45000.50;
            insertCmd.Parameters.Add(balParam);

            var rowsAffected = await insertCmd.ExecuteNonQueryAsync();
            rowsAffected.Should().Be(1);
        }

        // 3. Select and verify row
        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT Name, OpeningBalance FROM TestAccountingMasters WHERE Code = @Code;";

            var codeParam = selectCmd.CreateParameter();
            codeParam.ParameterName = "@Code";
            codeParam.Value = "SD-001";
            selectCmd.Parameters.Add(codeParam);

            await using var reader = await selectCmd.ExecuteReaderAsync();
            var hasRow = await reader.ReadAsync();

            hasRow.Should().BeTrue();
            reader.GetString(0).Should().Be("Sundry Debtors North");
            reader.GetDouble(1).Should().Be(45000.50);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            var walPath = _testDbPath + "-wal";
            if (File.Exists(walPath)) File.Delete(walPath);
            var shmPath = _testDbPath + "-shm";
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
        catch
        {
            // Best effort
        }
    }
}

`

---
## FILE: tests\BillsEasy.UnitTests\Application\Services\AccountGroupServiceTests.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class AccountGroupServiceTests
{
    private readonly IAccountGroupRepository _repo = Substitute.For<IAccountGroupRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly AccountGroupService _service;
    private readonly Guid _companyId = Guid.NewGuid();

    public AccountGroupServiceTests()
    {
        _userService.UserId.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new AccountGroupService(_repo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateGroup_WithValidData_PersistsAndReturnsDto()
    {
        _repo.ExistsByNameAsync(_companyId, "Custom Debtors", null, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateAccountGroupDto(_companyId, "Custom Debtors", null, GroupNature.Asset);
        var result = await _service.CreateGroupAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Custom Debtors");
        result.Nature.Should().Be(GroupNature.Asset);
        result.CreatedBy.Should().Be("test_user");

        await _repo.Received(1).AddAsync(Arg.Is<AccountGroup>(g => g.Name == "Custom Debtors"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WhenDuplicateSiblingNameExists_ThrowsDomainException()
    {
        _repo.ExistsByNameAsync(_companyId, "Duplicate Group", null, null, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreateAccountGroupDto(_companyId, "Duplicate Group", null, GroupNature.Asset);
        var act = async () => await _service.CreateGroupAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists under the selected parent group*");

        await _repo.DidNotReceive().AddAsync(Arg.Any<AccountGroup>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WhenSameNameUnderDifferentParent_IsAllowed()
    {
        var parent1 = Guid.NewGuid();
        var parentGroup1 = AccountGroup.Create(_companyId, "Current Liabilities", GroupNature.Liability, isPrimary: true);

        _repo.GetByIdAsync(_companyId, parent1, Arg.Any<CancellationToken>())
            .Returns(parentGroup1);
        _repo.ExistsByNameAsync(_companyId, "Outstanding Expenses", parent1, null, Arg.Any<CancellationToken>())
            .Returns(false); // Sibling check passes

        var dto = new CreateAccountGroupDto(_companyId, "Outstanding Expenses", parent1);
        var result = await _service.CreateGroupAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Outstanding Expenses");
        result.Nature.Should().Be(GroupNature.Liability);
        await _repo.Received(1).AddAsync(Arg.Is<AccountGroup>(g => g.Name == "Outstanding Expenses"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WithParentGroup_InheritsParentNature()
    {
        var parentId = Guid.NewGuid();
        var parentGroup = AccountGroup.Create(_companyId, "Fixed Assets", GroupNature.Asset, isPrimary: true);

        _repo.GetByIdAsync(_companyId, parentId, Arg.Any<CancellationToken>())
            .Returns(parentGroup);
        _repo.ExistsByNameAsync(_companyId, "Computers", parentId, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateAccountGroupDto(_companyId, "Computers", parentId, GroupNature.Expense);
        var result = await _service.CreateGroupAsync(dto);

        result.Nature.Should().Be(GroupNature.Asset); // Inherited from parent Fixed Assets
    }

    [Fact]
    public async Task CreateGroup_WhenParentBelongsToDifferentCompany_ThrowsDomainException()
    {
        var parentId = Guid.NewGuid();
        var parentGroup = AccountGroup.Create(Guid.NewGuid(), "Other Co Group", GroupNature.Asset);

        _repo.GetByIdAsync(_companyId, parentId, Arg.Any<CancellationToken>())
            .Returns(parentGroup);

        var dto = new CreateAccountGroupDto(_companyId, "Invalid Sub Group", parentId);
        var act = async () => await _service.CreateGroupAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*must belong to the same company*");
    }

    [Fact]
    public async Task UpdateGroup_WhenCircularHierarchyDetected_ThrowsDomainException()
    {
        // Hierarchy: Root(A) -> Child(B) -> SubChild(C)
        var rootA = AccountGroup.Create(_companyId, "Group A", GroupNature.Asset, isPrimary: true);
        var childB = AccountGroup.Create(_companyId, "Group B", GroupNature.Asset, parentGroupId: rootA.Id);
        var subChildC = AccountGroup.Create(_companyId, "Group C", GroupNature.Asset, parentGroupId: childB.Id);

        _repo.GetByIdAsync(_companyId, rootA.Id, Arg.Any<CancellationToken>()).Returns(rootA);
        _repo.GetByIdAsync(_companyId, subChildC.Id, Arg.Any<CancellationToken>()).Returns(subChildC);
        _repo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup> { rootA, childB, subChildC });

        // Attempt to make Root(A)'s parent SubChild(C) -> creating cycle A -> B -> C -> A
        var updateDto = new UpdateAccountGroupDto(rootA.Id, _companyId, "Group A", subChildC.Id);
        var act = async () => await _service.UpdateGroupAsync(updateDto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*circular hierarchy detected*");
    }

    [Fact]
    public async Task DeleteGroup_WhenProtectedSystemGroup_ThrowsDomainException()
    {
        var sysGroup = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset, isDefaultSystemGroup: true);
        _repo.GetByIdAsync(_companyId, sysGroup.Id, Arg.Any<CancellationToken>()).Returns(sysGroup);

        var act = async () => await _service.DeleteGroupAsync(_companyId, sysGroup.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*System predefined account group*cannot be deleted*");

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGroup_WhenHasChildren_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Parent Group", GroupNature.Asset);
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteGroupAsync(_companyId, group.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more child sub-groups*");

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGroup_WhenHasLedgers_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Active Group", GroupNature.Asset);
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(false);
        _repo.HasLedgersAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteGroupAsync(_companyId, group.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more ledgers assigned*");

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedDefaultGroups_WhenNoGroupsExist_SeedsTaxonomy()
    {
        _repo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup>());

        await _service.SeedDefaultGroupsAsync(_companyId, "admin");

        await _repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<AccountGroup>>(list => list.Any(g => g.Name == "Sundry Debtors") &&
                                                      list.Any(g => g.Name == "Sundry Creditors") &&
                                                      list.Any(g => g.Name == "Bank Accounts") &&
                                                      list.Any(g => g.Name == "Fixed Assets") &&
                                                      list.Any(g => g.Name == "Direct Expenses") &&
                                                      list.Any(g => g.Name == "Sales Accounts")),
            Arg.Any<CancellationToken>());
    }
}
`

---
## FILE: tests\BillsEasy.UnitTests\Application\Services\CompanyServiceTests.cs
`csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class CompanyServiceTests
{
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly IFinancialYearRepository _fyRepo = Substitute.For<IFinancialYearRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly CompanyService _service;

    public CompanyServiceTests()
    {
        _userService.UserId.Returns("admin");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

        _service = new CompanyService(_companyRepo, _fyRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateCompany_WhenNameAlreadyExists_ThrowsDomainException()
    {
        _companyRepo.ExistsByNameAsync("Existing Co", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await _service.CreateCompanyAsync(
            "Existing Co", "Existing Co", null, "Gujarat", "24",
            new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");

        await _companyRepo.DidNotReceive().AddAsync(Arg.Any<Company>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCompany_WithValidData_SetsAuditFieldsAndPersists()
    {
        _companyRepo.ExistsByNameAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.CreateCompanyAsync(
            "New Enterprise", "New Enterprise", "NE", "Gujarat", "24",
            new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1),
            gstin: "24AAACN1234A1Z5");

        result.Should().NotBeNull();
        result.CompanyName.Should().Be("New Enterprise");
        result.CreatedBy.Should().Be("admin");
        result.CreatedAt.Should().Be(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

        await _companyRepo.Received(1).AddAsync(
            Arg.Is<Company>(c => c.CompanyName == "New Enterprise" && c.CreatedBy == "admin"),
            Arg.Any<CancellationToken>());
    }
}

`

---
## FILE: tests\BillsEasy.UnitTests\Application\Services\FinancialYearServiceTests.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class FinancialYearServiceTests
{
    private readonly IFinancialYearRepository _fyRepo = Substitute.For<IFinancialYearRepository>();
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly FinancialYearService _service;

    public FinancialYearServiceTests()
    {
        _userService.UserId.Returns("admin");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

        _service = new FinancialYearService(_fyRepo, _companyRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateFinancialYear_WhenCompanyDoesNotExist_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns((Company?)null);

        var act = async () => await _service.CreateFinancialYearAsync(
            companyId, new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateFinancialYear_WhenOverlapsWithExisting_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        var company = Company.Create("Co", "Co", null, "Gujarat", "24", new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));
        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>()).Returns(company);

        var existingFy = FinancialYear.Create(companyId, new DateOnly(2024, 4, 1), new DateOnly(2025, 3, 31), "2024-2025");
        _fyRepo.GetByCompanyIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(new List<FinancialYear> { existingFy });

        // Overlapping dates
        var act = async () => await _service.CreateFinancialYearAsync(
            companyId, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*overlaps with existing Financial Year*");
    }
}

`

---
## FILE: tests\BillsEasy.UnitTests\Application\Services\LedgerServiceTests.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class LedgerServiceTests
{
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly IAccountGroupRepository _groupRepo = Substitute.For<IAccountGroupRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly LedgerService _service;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public LedgerServiceTests()
    {
        _userService.UserId.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new LedgerService(_ledgerRepo, _groupRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateLedger_WithValidData_PersistsAndReturnsDto()
    {
        var group = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset, isPrimary: false);
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns(group);
        _ledgerRepo.ExistsByNameAsync(_companyId, "Ramesh Traders", null, Arg.Any<CancellationToken>()).Returns(false);

        var dto = new CreateLedgerDto(
            _companyId,
            _groupId,
            "Ramesh Traders",
            PrintName: "Ramesh Traders Pvt Ltd",
            OpeningBalance: 12500m,
            OpeningBalanceType: DebitCreditType.Debit,
            BillWiseEnabled: true,
            GSTIN: "24AAACR1234A1Z5");

        var result = await _service.CreateLedgerAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Ramesh Traders");
        result.PrintName.Should().Be("Ramesh Traders Pvt Ltd");
        result.GroupName.Should().Be("Sundry Debtors");
        result.OpeningBalance.Should().Be(12500m);
        result.CreatedBy.Should().Be("test_user");

        await _ledgerRepo.Received(1).AddAsync(Arg.Is<Ledger>(l => l.Name == "Ramesh Traders"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLedger_WhenAccountGroupDoesNotExist_ThrowsDomainException()
    {
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns((AccountGroup?)null);

        var dto = new CreateLedgerDto(_companyId, _groupId, "Invalid Group Ledger");
        var act = async () => await _service.CreateLedgerAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");

        await _ledgerRepo.DidNotReceive().AddAsync(Arg.Any<Ledger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLedger_WhenAccountGroupBelongsToDifferentCompany_ThrowsDomainException()
    {
        var foreignGroup = AccountGroup.Create(Guid.NewGuid(), "Other Co Group", GroupNature.Asset);
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns(foreignGroup);

        var dto = new CreateLedgerDto(_companyId, _groupId, "Cross Co Ledger");
        var act = async () => await _service.CreateLedgerAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*must belong to the same company*");

        await _ledgerRepo.DidNotReceive().AddAsync(Arg.Any<Ledger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLedger_WhenDuplicateNameExistsInSameCompany_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset);
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns(group);
        _ledgerRepo.ExistsByNameAsync(_companyId, "Duplicate Ledger", null, Arg.Any<CancellationToken>()).Returns(true);

        var dto = new CreateLedgerDto(_companyId, _groupId, "Duplicate Ledger");
        var act = async () => await _service.CreateLedgerAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists in this company*");

        await _ledgerRepo.DidNotReceive().AddAsync(Arg.Any<Ledger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenProtectedSystemLedger_ThrowsDomainException()
    {
        var sysLedger = Ledger.Create(_companyId, _groupId, "Cash", isDefaultSystemLedger: true);
        _ledgerRepo.GetByIdAsync(_companyId, sysLedger.Id, Arg.Any<CancellationToken>()).Returns(sysLedger);

        var act = async () => await _service.DeleteLedgerAsync(_companyId, sysLedger.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*System predefined ledger*cannot be deleted*");

        await _ledgerRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenHasTransactions_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Active Customer");
        _ledgerRepo.GetByIdAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        _ledgerRepo.HasTransactionsAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteLedgerAsync(_companyId, ledger.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more recorded financial transactions*");

        await _ledgerRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenHasBillReferences_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Party With Bills");
        _ledgerRepo.GetByIdAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        _ledgerRepo.HasTransactionsAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.HasBillReferencesAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteLedgerAsync(_companyId, ledger.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more bill-wise references*");

        await _ledgerRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenUnusedUserCreatedLedger_DeletesSuccessfully()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Unused Supplier", isDefaultSystemLedger: false);
        _ledgerRepo.GetByIdAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        _ledgerRepo.HasTransactionsAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.HasBillReferencesAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(false);

        await _service.DeleteLedgerAsync(_companyId, ledger.Id);

        await _ledgerRepo.Received(1).DeleteAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedDefaultLedgers_WhenNoLedgersExist_SeedsCashAndProfitLoss()
    {
        _ledgerRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<Ledger>());

        var cashGroup = AccountGroup.Create(_companyId, "Cash-in-hand", GroupNature.Asset, isDefaultSystemGroup: true);
        var plGroup = AccountGroup.Create(_companyId, "Capital Account", GroupNature.Capital, isDefaultSystemGroup: true);

        _groupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup> { cashGroup, plGroup });

        await _service.SeedDefaultLedgersAsync(_companyId, "admin");

        await _ledgerRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<Ledger>>(list => list.Any(l => l.Name == "Cash" && l.IsDefaultSystemLedger) &&
                                                list.Any(l => l.Name == "Profit & Loss Account" && l.IsDefaultSystemLedger)),
            Arg.Any<CancellationToken>());
    }
}
`

---
## FILE: tests\BillsEasy.UnitTests\BillsEasy.UnitTests.csproj
`xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BillsEasy.Domain\BillsEasy.Domain.csproj" />
    <ProjectReference Include="..\..\src\BillsEasy.Application\BillsEasy.Application.csproj" />
  </ItemGroup>

</Project>

`

---
## FILE: tests\BillsEasy.UnitTests\Domain\Common\AuditableEntityTests.cs
`csharp
using System;
using BillsEasy.Domain.Common;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Common;

public class TestEntity : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;

    public TestEntity(Guid id, string name) : base(id)
    {
        Name = name;
    }
}

public class AuditableEntityTests
{
    [Fact]
    public void Entities_WithSameIdAndType_AreEqual()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "Alpha");
        var e2 = new TestEntity(id, "Beta");

        (e1 == e2).Should().BeTrue();
        e1.Equals(e2).Should().BeTrue();
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    [Fact]
    public void Entities_WithDifferentIds_AreNotEqual()
    {
        var e1 = new TestEntity(Guid.NewGuid(), "Alpha");
        var e2 = new TestEntity(Guid.NewGuid(), "Alpha");

        (e1 == e2).Should().BeFalse();
        e1.Equals(e2).Should().BeFalse();
    }

    [Fact]
    public void AuditableProperties_CanBeSetAndRetrieved()
    {
        var now = DateTime.UtcNow;
        var entity = new TestEntity(Guid.NewGuid(), "Test")
        {
            CreatedAt = now,
            CreatedBy = "admin",
            ModifiedAt = now.AddMinutes(5),
            ModifiedBy = "manager"
        };

        entity.CreatedAt.Should().Be(now);
        entity.CreatedBy.Should().Be("admin");
        entity.ModifiedAt.Should().Be(now.AddMinutes(5));
        entity.ModifiedBy.Should().Be("manager");
    }
}

`

---
## FILE: tests\BillsEasy.UnitTests\Domain\Entities\AccountGroupTests.cs
`csharp
using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class AccountGroupTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsAccountGroup()
    {
        var group = AccountGroup.Create(
            _companyId,
            "Sundry Debtors",
            GroupNature.Asset,
            parentGroupId: null,
            isPrimary: true,
            isDefaultSystemGroup: false);

        group.Should().NotBeNull();
        group.Id.Should().NotBeEmpty();
        group.CompanyId.Should().Be(_companyId);
        group.Name.Should().Be("Sundry Debtors");
        group.Nature.Should().Be(GroupNature.Asset);
        group.ParentGroupId.Should().BeNull();
        group.IsPrimary.Should().BeTrue();
        group.IsDefaultSystemGroup.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => AccountGroup.Create(
            _companyId,
            invalidName!,
            GroupNature.Asset);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => AccountGroup.Create(
            Guid.Empty,
            "Valid Name",
            GroupNature.Asset);

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Constructor_WhenParentGroupIdEqualsId_ThrowsDomainException()
    {
        var id = Guid.NewGuid();
        Action act = () => new AccountGroup(
            id,
            _companyId,
            "Self Parent Group",
            GroupNature.Expense,
            parentGroupId: id);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be its own parent*");
    }

    [Fact]
    public void Reparent_WithValidParent_InheritsNatureAndSetsParentId()
    {
        var parent = AccountGroup.Create(_companyId, "Current Assets", GroupNature.Asset, isPrimary: true);
        var group = AccountGroup.Create(_companyId, "Bank Accounts", GroupNature.Liability, isPrimary: true);
        var hierarchy = new[] { parent, group };

        group.Reparent(parent, hierarchy);

        group.ParentGroupId.Should().Be(parent.Id);
        group.Nature.Should().Be(GroupNature.Asset);
        group.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Reparent_WhenParentFromDifferentCompany_ThrowsDomainException()
    {
        var parent = AccountGroup.Create(Guid.NewGuid(), "Other Company Group", GroupNature.Asset);
        var group = AccountGroup.Create(_companyId, "My Group", GroupNature.Asset);
        var hierarchy = new[] { parent, group };

        Action act = () => group.Reparent(parent, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*Parent account group must belong to the same company*");
    }

    [Fact]
    public void Reparent_WhenSelfParenting_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "My Group", GroupNature.Asset);
        var hierarchy = new[] { group };

        Action act = () => group.Reparent(group, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be its own parent*");
    }

    [Fact]
    public void Reparent_WhenCircularHierarchyDetected_ThrowsDomainException()
    {
        // A (Root) -> B (Child of A) -> C (Child of B)
        // Attempting to make A a child of C must throw!
        var groupA = AccountGroup.Create(_companyId, "Group A", GroupNature.Asset, isPrimary: true);
        var groupB = AccountGroup.Create(_companyId, "Group B", GroupNature.Asset, parentGroupId: groupA.Id);
        var groupC = AccountGroup.Create(_companyId, "Group C", GroupNature.Asset, parentGroupId: groupB.Id);
        var hierarchy = new[] { groupA, groupB, groupC };

        Action act = () => groupA.Reparent(groupC, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*circular hierarchy detected*");
    }

    [Fact]
    public void Reparent_WhenNullParent_SetsRootAndPrimary()
    {
        var parentId = Guid.NewGuid();
        var group = AccountGroup.Create(_companyId, "Sub Group", GroupNature.Asset, parentGroupId: parentId);
        var hierarchy = new[] { group };

        group.Reparent(null, hierarchy);

        group.ParentGroupId.Should().BeNull();
        group.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Update_WhenModifyingProtectedSystemGroupNature_ThrowsDomainException()
    {
        var sysGroup = AccountGroup.Create(_companyId, "Fixed Assets", GroupNature.Asset, isDefaultSystemGroup: true);
        var hierarchy = new[] { sysGroup };

        Action act = () => sysGroup.Update("Fixed Assets", null, hierarchy, GroupNature.Liability);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot change the nature of protected system group*");
    }

    [Fact]
    public void MethodSignatures_RequireExplicitCompanyGroupsParameter_WithNoDefaultValue()
    {
        var reparentMethod = typeof(AccountGroup).GetMethod(nameof(AccountGroup.Reparent));
        reparentMethod.Should().NotBeNull();
        var companyGroupsParam1 = Array.Find(reparentMethod!.GetParameters(), p => p.Name == "companyGroups");
        companyGroupsParam1.Should().NotBeNull();
        companyGroupsParam1!.HasDefaultValue.Should().BeFalse("companyGroups parameter in Reparent must be mandatory with no default value");

        var updateMethod = typeof(AccountGroup).GetMethod(nameof(AccountGroup.Update));
        updateMethod.Should().NotBeNull();
        var companyGroupsParam2 = Array.Find(updateMethod!.GetParameters(), p => p.Name == "companyGroups");
        companyGroupsParam2.Should().NotBeNull();
        companyGroupsParam2!.HasDefaultValue.Should().BeFalse("companyGroups parameter in Update must be mandatory with no default value");
    }

    [Fact]
    public void SetPrimary_WhenHasParent_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Sub Group", GroupNature.Asset, parentGroupId: Guid.NewGuid());

        Action act = () => group.SetPrimary(true);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be designated as a Primary Root group*");
    }

    [Fact]
    public void Reparent_WhenAncestorNotFoundInCompanyGroups_ThrowsDomainException()
    {
        var groupA = AccountGroup.Create(_companyId, "Group A", GroupNature.Asset, isPrimary: true);
        var missingParentId = Guid.NewGuid();
        var groupB = AccountGroup.Create(_companyId, "Group B", GroupNature.Asset, parentGroupId: missingParentId);
        
        // Incomplete collection missing missingParentId
        var incompleteHierarchy = new[] { groupA, groupB };

        Action act = () => groupA.Reparent(groupB, incompleteHierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*Incomplete account group hierarchy supplied*");
    }

    [Fact]
    public void Reparent_WhenTargetParentHasDifferentNatureAndGroupHasDescendants_ThrowsDomainException()
    {
        var assetRoot = AccountGroup.Create(_companyId, "Assets", GroupNature.Asset, isPrimary: true);
        var subAsset = AccountGroup.Create(_companyId, "Sub Assets", GroupNature.Asset, parentGroupId: assetRoot.Id);
        var childAsset = AccountGroup.Create(_companyId, "Child Assets", GroupNature.Asset, parentGroupId: subAsset.Id);
        var liabilityRoot = AccountGroup.Create(_companyId, "Liabilities", GroupNature.Liability, isPrimary: true);

        var hierarchy = new[] { assetRoot, subAsset, childAsset, liabilityRoot };

        // Attempting to reparent subAsset (which has childAsset descendant) under liabilityRoot must fail!
        Action act = () => subAsset.Reparent(liabilityRoot, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*because it has child sub-groups*");
    }

    [Fact]
    public void Reparent_WhenLeafGroupReparentedToDifferentNature_SucceedsAndDerivesParentNature()
    {
        var assetRoot = AccountGroup.Create(_companyId, "Assets", GroupNature.Asset, isPrimary: true);
        var leaf = AccountGroup.Create(_companyId, "Misc Group", GroupNature.Asset, parentGroupId: assetRoot.Id);
        var liabilityRoot = AccountGroup.Create(_companyId, "Liabilities", GroupNature.Liability, isPrimary: true);

        var hierarchy = new[] { assetRoot, leaf, liabilityRoot };

        // Leaf group has no descendants, reparenting to Liabilities should succeed and change nature to Liability
        leaf.Reparent(liabilityRoot, hierarchy);

        leaf.ParentGroupId.Should().Be(liabilityRoot.Id);
        leaf.Nature.Should().Be(GroupNature.Liability);
    }

    [Fact]
    public void Update_WhenRootGroupChangesNatureWithDescendants_ThrowsDomainException()
    {
        var root = AccountGroup.Create(_companyId, "Custom Root", GroupNature.Asset, isPrimary: true);
        var child = AccountGroup.Create(_companyId, "Custom Child", GroupNature.Asset, parentGroupId: root.Id);

        var hierarchy = new[] { root, child };

        Action act = () => root.Update("Custom Root", null, hierarchy, GroupNature.Expense);

        act.Should().Throw<DomainException>()
            .WithMessage("*because it has child sub-groups*");
    }

    [Fact]
    public void Update_WhenLeafRootGroupChangesNature_Succeeds()
    {
        var root = AccountGroup.Create(_companyId, "Custom Root", GroupNature.Asset, isPrimary: true);
        var hierarchy = new[] { root };

        root.Update("Custom Root Renamed", null, hierarchy, GroupNature.Income);

        root.Nature.Should().Be(GroupNature.Income);
        root.Name.Should().Be("Custom Root Renamed");
    }
}
`

---
## FILE: tests\BillsEasy.UnitTests\Domain\Entities\CompanyTests.cs
`csharp
using System;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class CompanyTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesCompanyAndInitialFinancialYear()
    {
        var fyStart = new DateOnly(2024, 4, 1);
        var booksStart = new DateOnly(2024, 4, 1);

        var company = Company.Create(
            companyName: "Ramesh Trading Co",
            printName: "Ramesh Trading Company Pvt Ltd",
            shortName: "RTC",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: fyStart,
            booksBeginningFrom: booksStart,
            gstin: "24AAACR1234A1Z5",
            gstRegistrationType: GSTRegistrationType.Regular);

        company.Id.Should().NotBeEmpty();
        company.CompanyName.Should().Be("Ramesh Trading Co");
        company.PrintName.Should().Be("Ramesh Trading Company Pvt Ltd");
        company.ShortName.Should().Be("RTC");
        company.StateName.Should().Be("Gujarat");
        company.StateCode.Should().Be("24");
        company.GSTIN.Should().Be("24AAACR1234A1Z5");
        company.CurrencySymbol.Should().Be("₹");
        company.CurrencyFormalName.Should().Be("INR");

        company.FinancialYears.Should().HaveCount(1);
        var initialFy = company.FinancialYears.First();
        initialFy.StartDate.Should().Be(new DateOnly(2024, 4, 1));
        initialFy.EndDate.Should().Be(new DateOnly(2025, 3, 31));
        initialFy.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsDomainException(string? invalidName)
    {
        var fyStart = new DateOnly(2024, 4, 1);

        Action act = () => Company.Create(
            companyName: invalidName!,
            printName: "Print",
            shortName: null,
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: fyStart,
            booksBeginningFrom: fyStart);

        act.Should().Throw<DomainException>()
            .WithMessage("*Company name cannot be empty*");
    }

    [Fact]
    public void Create_WhenBooksBeginningBeforeFinancialYearBeginning_ThrowsDomainException()
    {
        var fyStart = new DateOnly(2024, 4, 1);
        var invalidBooksStart = new DateOnly(2024, 3, 31);

        Action act = () => Company.Create(
            companyName: "Test Co",
            printName: "Test Co",
            shortName: null,
            stateName: "Maharashtra",
            stateCode: "27",
            financialYearBeginning: fyStart,
            booksBeginningFrom: invalidBooksStart);

        act.Should().Throw<DomainException>()
            .WithMessage("*Books beginning date*cannot be earlier than Financial year beginning date*");
    }

    [Theory]
    [InlineData("24AAACR1234A1")] // Too short
    [InlineData("24AAACR1234A1Z567")] // Too long
    [InlineData("INVALIDGSTIN1234")] // Non-matching pattern
    public void Create_WithInvalidGSTIN_ThrowsDomainException(string invalidGstin)
    {
        var fyStart = new DateOnly(2024, 4, 1);

        Action act = () => Company.Create(
            companyName: "Test Co",
            printName: "Test Co",
            shortName: null,
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: fyStart,
            booksBeginningFrom: fyStart,
            gstin: invalidGstin);

        act.Should().Throw<DomainException>()
            .WithMessage("*Invalid GSTIN format*");
    }

    [Fact]
    public void Update_WithValidDetails_UpdatesCompanyProperties()
    {
        var fyStart = new DateOnly(2024, 4, 1);
        var company = Company.Create(
            "Original Name", "Orig Print", "ON", "Gujarat", "24", fyStart, fyStart);

        company.Update("New Name", "New Print", "NN", "Maharashtra", "27", "27AAACR1234A1Z5", GSTRegistrationType.Composition, true);

        company.CompanyName.Should().Be("New Name");
        company.PrintName.Should().Be("New Print");
        company.ShortName.Should().Be("NN");
        company.StateName.Should().Be("Maharashtra");
        company.StateCode.Should().Be("27");
        company.GSTIN.Should().Be("27AAACR1234A1Z5");
        company.GSTRegistrationType.Should().Be(GSTRegistrationType.Composition);
        company.IsSecurityEnabled.Should().BeTrue();
    }
}

`

---
## FILE: tests\BillsEasy.UnitTests\Domain\Entities\FinancialYearTests.cs
`csharp
using System;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class FinancialYearTests
{
    [Fact]
    public void Create_WithValidDates_SetsPropertiesCorrectly()
    {
        var companyId = Guid.NewGuid();
        var start = new DateOnly(2024, 4, 1);
        var end = new DateOnly(2025, 3, 31);

        var fy = FinancialYear.Create(companyId, start, end, "FY 2024-25", isActive: true);

        fy.CompanyId.Should().Be(companyId);
        fy.StartDate.Should().Be(start);
        fy.EndDate.Should().Be(end);
        fy.YearName.Should().Be("FY 2024-25");
        fy.IsActive.Should().BeTrue();
        fy.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenStartDateEqualsOrAfterEndDate_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        var start = new DateOnly(2024, 4, 1);
        var invalidEnd = new DateOnly(2024, 4, 1);

        Action act = () => new FinancialYear(Guid.NewGuid(), companyId, "2024", start, invalidEnd);

        act.Should().Throw<DomainException>()
            .WithMessage("*StartDate*must precede EndDate*");
    }

    [Fact]
    public void Contains_ReturnsTrueForDatesWithinRange()
    {
        var fy = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2024, 4, 1), new DateOnly(2025, 3, 31));

        fy.Contains(new DateOnly(2024, 4, 1)).Should().BeTrue();
        fy.Contains(new DateOnly(2024, 10, 15)).Should().BeTrue();
        fy.Contains(new DateOnly(2025, 3, 31)).Should().BeTrue();
        fy.Contains(new DateOnly(2024, 3, 31)).Should().BeFalse();
        fy.Contains(new DateOnly(2025, 4, 1)).Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_ReturnsTrueForOverlappingPeriods()
    {
        var fy1 = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2024, 4, 1), new DateOnly(2025, 3, 31));
        var overlapping = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2024, 10, 1), new DateOnly(2025, 9, 30));
        var nonOverlapping = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31));

        fy1.OverlapsWith(overlapping).Should().BeTrue();
        fy1.OverlapsWith(nonOverlapping).Should().BeFalse();
    }

    [Fact]
    public void AddFinancialYear_WhenOverlapsWithExistingYear_ThrowsDomainException()
    {
        var company = Company.Create("Test Co", "Test", null, "Gujarat", "24", new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));

        var overlappingFy = FinancialYear.Create(company.Id, new DateOnly(2024, 6, 1), new DateOnly(2025, 5, 31), "2024-2025-Overlap");

        Action act = () => company.AddFinancialYear(overlappingFy);

        act.Should().Throw<DomainException>()
            .WithMessage("*overlaps with existing Financial Year*");
    }

    [Fact]
    public void AddFinancialYear_WhenValidContiguousPeriod_AddsSuccessfully()
    {
        var company = Company.Create("Test Co", "Test", null, "Gujarat", "24", new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));

        var nextFy = FinancialYear.Create(company.Id, new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31), "2025-2026", isActive: true);

        company.AddFinancialYear(nextFy);

        company.FinancialYears.Should().HaveCount(2);
        company.FinancialYears.First().IsActive.Should().BeFalse();
        company.FinancialYears.Last().IsActive.Should().BeTrue();
    }
}

`

---
## FILE: tests\BillsEasy.UnitTests\Domain\Entities\LedgerTests.cs
`csharp
using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class LedgerTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsLedger()
    {
        var ledger = Ledger.Create(
            _companyId,
            _groupId,
            "Rohit Enterprises",
            printName: "Rohit Enterprises Pvt Ltd",
            alias: "ROHIT",
            openingBalance: 15000.50m,
            openingBalanceType: DebitCreditType.Debit,
            billWiseEnabled: true,
            creditPeriodDays: 30,
            creditLimit: 50000m,
            city: "Ahmedabad",
            stateName: "Gujarat",
            stateCode: "24",
            gstin: "24AAACR1234A1Z5");

        ledger.Should().NotBeNull();
        ledger.Id.Should().NotBeEmpty();
        ledger.CompanyId.Should().Be(_companyId);
        ledger.GroupId.Should().Be(_groupId);
        ledger.Name.Should().Be("Rohit Enterprises");
        ledger.PrintName.Should().Be("Rohit Enterprises Pvt Ltd");
        ledger.Alias.Should().Be("ROHIT");
        ledger.OpeningBalance.Should().Be(new Money(15000.50m));
        ledger.OpeningBalanceType.Should().Be(DebitCreditType.Debit);
        ledger.BillWiseEnabled.Should().BeTrue();
        ledger.CreditPeriodDays.Should().Be(30);
        ledger.CreditLimit.Should().Be(new Money(50000m));
        ledger.City.Should().Be("Ahmedabad");
        ledger.StateName.Should().Be("Gujarat");
        ledger.StateCode.Should().Be("24");
        ledger.GSTIN.Should().Be("24AAACR1234A1Z5");
        ledger.IsDefaultSystemLedger.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => Ledger.Create(_companyId, _groupId, invalidName!);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(Guid.Empty, _groupId, "Valid Ledger");

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyGroupId_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, Guid.Empty, "Valid Ledger");

        act.Should().Throw<DomainException>()
            .WithMessage("*GroupId cannot be empty*");
    }

    [Fact]
    public void Create_WithNegativeOpeningBalance_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: -100m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Opening balance cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativeCreditPeriod_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", creditPeriodDays: -5);

        act.Should().Throw<DomainException>()
            .WithMessage("*Credit period days cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativeCreditLimit_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", creditLimit: -500m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Credit limit cannot be negative*");
    }

    [Theory]
    [InlineData("24AAACR1234A1")]
    [InlineData("INVALIDGSTIN1234")]
    [InlineData("24AAACR1234A1Z567")]
    public void Create_WithInvalidGSTIN_ThrowsDomainException(string invalidGstin)
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", gstin: invalidGstin);

        act.Should().Throw<DomainException>()
            .WithMessage("*Invalid GSTIN format*");
    }

    [Fact]
    public void Create_WhenPrintNameIsEmpty_DefaultsToName()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "My Bank Account", printName: "  ");

        ledger.PrintName.Should().Be("My Bank Account");
    }

    [Fact]
    public void UpdateOpeningBalance_WhenNoTransactionHistory_UpdatesSuccessfully()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        ledger.UpdateOpeningBalance(500m, DebitCreditType.Credit, hasTransactionHistory: false);

        ledger.OpeningBalance.Should().Be(new Money(500m));
        ledger.OpeningBalanceType.Should().Be(DebitCreditType.Credit);
    }

    [Fact]
    public void UpdateOpeningBalance_WhenHasTransactionHistoryAndAmountChanged_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        Action act = () => ledger.UpdateOpeningBalance(200m, DebitCreditType.Debit, hasTransactionHistory: true);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify Opening Balance for a ledger that has recorded transaction history*");
    }

    [Fact]
    public void UpdateOpeningBalance_WhenHasTransactionHistoryAndTypeChanged_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        Action act = () => ledger.UpdateOpeningBalance(100m, DebitCreditType.Credit, hasTransactionHistory: true);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify Opening Balance for a ledger that has recorded transaction history*");
    }

    [Fact]
    public void UpdateOpeningBalance_WhenHasTransactionHistoryAndUnchanged_Succeeds()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        ledger.UpdateOpeningBalance(100m, DebitCreditType.Debit, hasTransactionHistory: true);

        ledger.OpeningBalance.Should().Be(new Money(100m));
    }

    [Fact]
    public void Update_WhenHasTransactionHistoryAndOpeningBalanceChanged_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        Action act = () => ledger.Update(
            _groupId,
            "Updated Name",
            hasTransactionHistory: true,
            openingBalance: 200m,
            openingBalanceType: DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify Opening Balance for a ledger that has recorded transaction history*");
    }

    [Fact]
    public void MethodSignatures_RequireExplicitHasTransactionHistoryParameter_WithNoDefaultValue()
    {
        var updateOpBalMethod = typeof(Ledger).GetMethod(nameof(Ledger.UpdateOpeningBalance));
        updateOpBalMethod.Should().NotBeNull();
        var hasHistoryParam1 = Array.Find(updateOpBalMethod!.GetParameters(), p => p.Name == "hasTransactionHistory");
        hasHistoryParam1.Should().NotBeNull();
        hasHistoryParam1!.HasDefaultValue.Should().BeFalse("hasTransactionHistory parameter must be mandatory with no default value");

        var updateMethod = typeof(Ledger).GetMethod(nameof(Ledger.Update));
        updateMethod.Should().NotBeNull();
        var hasHistoryParam2 = Array.Find(updateMethod!.GetParameters(), p => p.Name == "hasTransactionHistory");
        hasHistoryParam2.Should().NotBeNull();
        hasHistoryParam2!.HasDefaultValue.Should().BeFalse("hasTransactionHistory parameter in Update must be mandatory with no default value");
    }
}
`

---
## FILE: tests\BillsEasy.UnitTests\Domain\ValueObjects\MoneyTests.cs
`csharp
using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmountAndDefaultCurrency_SetsPropertiesCorrectly()
    {
        var money = new Money(1500.50m);

        money.Amount.Should().Be(1500.50m);
        money.Currency.Should().Be("INR");
        money.IsZero.Should().BeFalse();
        money.IsPositive.Should().BeTrue();
        money.IsNegative.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithNullOrWhitespaceCurrency_ThrowsDomainException(string? invalidCurrency)
    {
        Action act = () => new Money(100m, invalidCurrency!);

        act.Should().Throw<DomainException>()
            .WithMessage("*Currency code cannot be null or empty*");
    }

    [Fact]
    public void Constructor_RoundsToTwoDecimalPlacesAwayFromZero()
    {
        var money = new Money(100.555m);
        money.Amount.Should().Be(100.56m);

        var money2 = new Money(100.554m);
        money2.Amount.Should().Be(100.55m);
    }

    [Fact]
    public void Zero_ReturnsZeroAmount()
    {
        var zero = Money.Zero();

        zero.Amount.Should().Be(0m);
        zero.IsZero.Should().BeTrue();
        zero.IsPositive.Should().BeFalse();
        zero.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void Equality_WithSameAmountAndCurrency_ReturnsTrue()
    {
        var m1 = new Money(500.25m, "INR");
        var m2 = new Money(500.25m, "INR");

        (m1 == m2).Should().BeTrue();
        (m1 != m2).Should().BeFalse();
        m1.Equals(m2).Should().BeTrue();
        m1.GetHashCode().Should().Be(m2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentAmountsOrCurrencies_ReturnsFalse()
    {
        var m1 = new Money(500.25m, "INR");
        var m2 = new Money(600.00m, "INR");
        var m3 = new Money(500.25m, "USD");

        (m1 == m2).Should().BeFalse();
        (m1 == m3).Should().BeFalse();
        m1.Equals(m2).Should().BeFalse();
        m1.Equals(m3).Should().BeFalse();
    }

    [Fact]
    public void Addition_WithSameCurrency_ReturnsCorrectSum()
    {
        var m1 = new Money(1200.50m);
        var m2 = new Money(800.25m);

        var result = m1 + m2;

        result.Amount.Should().Be(2000.75m);
        result.Currency.Should().Be("INR");
    }

    [Fact]
    public void Addition_WithDifferentCurrency_ThrowsDomainException()
    {
        var m1 = new Money(100m, "INR");
        var m2 = new Money(100m, "USD");

        Action act = () => { var _ = m1 + m2; };

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot perform currency operation between 'INR' and 'USD'*");
    }

    [Fact]
    public void Subtraction_WithSameCurrency_ReturnsCorrectDifference()
    {
        var m1 = new Money(1500m);
        var m2 = new Money(400m);

        var result = m1 - m2;

        result.Amount.Should().Be(1100m);
    }

    [Fact]
    public void UnaryNegation_ReturnsNegativeAmount()
    {
        var money = new Money(250m);
        var negated = -money;

        negated.Amount.Should().Be(-250m);
        negated.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Multiplication_WithDecimal_MultipliesCorrectly()
    {
        var money = new Money(150m);
        var result = money * 3m;

        result.Amount.Should().Be(450m);

        var result2 = 2.5m * money;
        result2.Amount.Should().Be(375m);
    }

    [Fact]
    public void Division_WithValidDivisor_DividesCorrectly()
    {
        var money = new Money(1000m);
        var result = money / 4m;

        result.Amount.Should().Be(250m);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        var money = new Money(1000m);

        Action act = () => { var _ = money / 0m; };

        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void Comparison_Operators_WorkCorrectly()
    {
        var low = new Money(100m);
        var high = new Money(500m);
        var same = new Money(100m);

        (low < high).Should().BeTrue();
        (low <= high).Should().BeTrue();
        (low <= same).Should().BeTrue();
        (high > low).Should().BeTrue();
        (high >= low).Should().BeTrue();
        (low >= same).Should().BeTrue();
    }

    [Fact]
    public void ToString_WithDefaultCurrency_FormatsWithIndianRupeeSymbol()
    {
        var money = new Money(150000.50m);
        var formatted = money.ToString();

        formatted.Should().Be("₹ 150,000.50");
    }

    [Fact]
    public void ToMinorUnits_ReturnsExactPaiseInteger()
    {
        var money = new Money(1234.56m);
        money.ToMinorUnits().Should().Be(123456L);

        var zero = Money.Zero();
        zero.ToMinorUnits().Should().Be(0L);

        var small = new Money(0.05m);
        small.ToMinorUnits().Should().Be(5L);
    }

    [Fact]
    public void FromMinorUnits_ReconstructsExactMoneyAmount()
    {
        var money = Money.FromMinorUnits(123456L);
        money.Amount.Should().Be(1234.56m);
        money.Currency.Should().Be("INR");

        var zero = Money.FromMinorUnits(0L);
        zero.Amount.Should().Be(0.00m);
    }

    [Fact]
    public void SummationOfFractions_HasZeroFloatDrift()
    {
        // Adding 0.10 three times in IEEE float produces 0.30000000000000004
        // Money value object guarantees exact 0.30m with zero drift
        var m1 = new Money(0.10m);
        var m2 = new Money(0.10m);
        var m3 = new Money(0.10m);

        var total = m1 + m2 + m3;
        total.Amount.Should().Be(0.30m);
        total.ToMinorUnits().Should().Be(30L);
    }
}

`


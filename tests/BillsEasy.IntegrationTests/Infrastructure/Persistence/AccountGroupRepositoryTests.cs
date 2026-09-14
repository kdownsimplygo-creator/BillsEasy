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
        // SCOPE: Verifies master setup cascade behavior for an EMPTY company (zero transaction history / no vouchers).
        // Once transactional vouchers exist, Company deletion is blocked by Application guard and DB RESTRICT FK.
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
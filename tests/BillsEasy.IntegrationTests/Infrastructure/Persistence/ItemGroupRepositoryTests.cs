using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class ItemGroupRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly ItemGroupRepository _groupRepository;

    public ItemGroupRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_ItmGrpTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _groupRepository = new ItemGroupRepository(_connectionFactory);
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
    public async Task AddAndGetById_PersistsItemGroupSuccessfully()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var group = ItemGroup.Create(company.Id, "Electronics", null, "All electronics");
        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";

        await _groupRepository.AddAsync(group);

        var retrieved = await _groupRepository.GetByIdAsync(company.Id, group.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(group.Id);
        retrieved.CompanyId.Should().Be(company.Id);
        retrieved.Name.Should().Be("Electronics");
        retrieved.ParentGroupId.Should().BeNull();
        retrieved.Description.Should().Be("All electronics");
    }

    [Fact]
    public async Task AddChildGroup_AndGetByParentId_ReturnsChildGroup()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var parent = ItemGroup.Create(company.Id, "Electronics");
        parent.CreatedAt = DateTime.UtcNow;
        parent.CreatedBy = "tester";
        await _groupRepository.AddAsync(parent);

        var child = ItemGroup.Create(company.Id, "Monitors", parent.Id);
        child.CreatedAt = DateTime.UtcNow;
        child.CreatedBy = "tester";
        await _groupRepository.AddAsync(child);

        var children = await _groupRepository.GetByParentIdAsync(company.Id, parent.Id);

        children.Should().ContainSingle();
        children[0].Name.Should().Be("Monitors");
        children[0].ParentGroupId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task HasChildren_ReturnsTrueWhenSubgroupsExist()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var parent = ItemGroup.Create(company.Id, "Electronics");
        parent.CreatedAt = DateTime.UtcNow;
        parent.CreatedBy = "tester";
        await _groupRepository.AddAsync(parent);

        var child = ItemGroup.Create(company.Id, "CPU", parent.Id);
        child.CreatedAt = DateTime.UtcNow;
        child.CreatedBy = "tester";
        await _groupRepository.AddAsync(child);

        var hasChildren = await _groupRepository.HasChildrenAsync(company.Id, parent.Id);
        hasChildren.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_RemovesItemGroup()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var group = ItemGroup.Create(company.Id, "Temporary");
        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";
        await _groupRepository.AddAsync(group);

        await _groupRepository.DeleteAsync(company.Id, group.Id);

        var retrieved = await _groupRepository.GetByIdAsync(company.Id, group.Id);
        retrieved.Should().BeNull();
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

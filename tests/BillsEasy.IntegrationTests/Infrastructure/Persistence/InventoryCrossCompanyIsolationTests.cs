using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using BillsEasy.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class InventoryCrossCompanyIsolationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly UnitRepository _unitRepository;
    private readonly ItemGroupRepository _groupRepository;
    private readonly ItemRepository _itemRepository;
    private readonly UnitService _unitService;
    private readonly ItemGroupService _groupService;
    private readonly ItemService _itemService;

    public InventoryCrossCompanyIsolationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_IsoTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _unitRepository = new UnitRepository(_connectionFactory);
        _groupRepository = new ItemGroupRepository(_connectionFactory);
        _itemRepository = new ItemRepository(_connectionFactory);

        var userService = new StandaloneCurrentUserService();
        var dateProvider = new DateTimeProvider();

        _unitService = new UnitService(_unitRepository, userService, dateProvider);
        _groupService = new ItemGroupService(_groupRepository, userService, dateProvider);
        _itemService = new ItemService(_itemRepository, _groupRepository, _unitRepository, userService, dateProvider);
    }

    private async Task<Company> CreateCompanyAsync(string name, string shortName)
    {
        var company = Company.Create(
            companyName: name,
            printName: name,
            shortName: shortName,
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
    public async Task SameNameAcrossCompanies_IsAllowedAndIsolated()
    {
        await _initializer.InitializeAsync();
        var companyA = await CreateCompanyAsync("Company Alpha", "CA");
        var companyB = await CreateCompanyAsync("Company Beta", "CB");

        // Both companies create unit "Pcs"
        var unitA = await _unitService.CreateUnitAsync(new CreateUnitDto(companyA.Id, "Pcs", "Pieces A", 0));
        var unitB = await _unitService.CreateUnitAsync(new CreateUnitDto(companyB.Id, "Pcs", "Pieces B", 2));

        unitA.CompanyId.Should().Be(companyA.Id);
        unitB.CompanyId.Should().Be(companyB.Id);

        var listA = await _unitService.GetAllUnitsAsync(companyA.Id);
        var listB = await _unitService.GetAllUnitsAsync(companyB.Id);

        listA.Should().ContainSingle(u => u.PrintName == "Pieces A");
        listA.Should().NotContain(u => u.PrintName == "Pieces B");

        listB.Should().ContainSingle(u => u.PrintName == "Pieces B");
        listB.Should().NotContain(u => u.PrintName == "Pieces A");
    }

    [Fact]
    public async Task ItemService_PreventsReferencingGroupOrUnitFromAnotherCompany()
    {
        await _initializer.InitializeAsync();
        var companyA = await CreateCompanyAsync("Company Alpha", "CA");
        var companyB = await CreateCompanyAsync("Company Beta", "CB");

        var unitA = await _unitService.CreateUnitAsync(new CreateUnitDto(companyA.Id, "Pcs"));
        var groupA = await _groupService.CreateGroupAsync(new CreateItemGroupDto(companyA.Id, "Electronics"));

        var unitB = await _unitService.CreateUnitAsync(new CreateUnitDto(companyB.Id, "Box"));
        var groupB = await _groupService.CreateGroupAsync(new CreateItemGroupDto(companyB.Id, "Hardware"));

        // Company A item tries to use Company B's group
        var actGroupLeak = async () => await _itemService.CreateItemAsync(new CreateItemDto(
            companyA.Id,
            groupB.Id,
            unitA.Id,
            "Leaked Item"));

        await actGroupLeak.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");

        // Company A item tries to use Company B's unit
        var actUnitLeak = async () => await _itemService.CreateItemAsync(new CreateItemDto(
            companyA.Id,
            groupA.Id,
            unitB.Id,
            "Leaked Item 2"));

        await actUnitLeak.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

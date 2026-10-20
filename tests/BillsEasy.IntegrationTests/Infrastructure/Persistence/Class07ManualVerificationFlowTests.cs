using System;
using System.IO;
using System.Linq;
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

public class Class07ManualVerificationFlowTests : IDisposable
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

    public Class07ManualVerificationFlowTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_C07Verif_{Guid.NewGuid():N}.db");
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

    [Fact]
    public async Task CompleteClass07MasterWorkflow_ExecutesSuccessfully()
    {
        await _initializer.InitializeAsync();

        // 1. Create Company
        var company = Company.Create(
            companyName: "Samsung Electronics India",
            printName: "Samsung Electronics India",
            shortName: "SEI",
            stateName: "Delhi",
            stateCode: "07",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));
        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "system";
        await _companyRepository.AddAsync(company);

        // 2. Units of Measurement: Create Pcs (0 decimals) and Box (2 decimals)
        var pcsUnit = await _unitService.CreateUnitAsync(new CreateUnitDto(company.Id, "Pcs", "Pieces", 0));
        var boxUnit = await _unitService.CreateUnitAsync(new CreateUnitDto(company.Id, "Box", "Boxes", 2));

        pcsUnit.Should().NotBeNull();
        boxUnit.Should().NotBeNull();

        // 3. Item Groups: Create "Electronics" root and "Monitors" child
        var electronicsGroup = await _groupService.CreateGroupAsync(new CreateItemGroupDto(company.Id, "Electronics", null, "All electronics"));
        var monitorsGroup = await _groupService.CreateGroupAsync(new CreateItemGroupDto(company.Id, "Monitors", electronicsGroup.Id, "Computer monitors"));

        monitorsGroup.ParentGroupId.Should().Be(electronicsGroup.Id);

        // 4. Create Item: "Samsung M25" with specified test data
        var itemDto = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: company.Id,
            ItemGroupId: monitorsGroup.Id,
            UnitId: pcsUnit.Id,
            Name: "Samsung M25",
            PrintName: "Samsung Galaxy M25",
            Code: "SM-M25-BLK",
            AlternateUnitId: boxUnit.Id,
            ConversionFactor: 10m,
            OpeningStockQuantity: 10,
            OpeningStockRate: 18000,
            OpeningStockValue: 180000,
            SalesPrice: 20000,
            PurchasePrice: 17500,
            MRP: 22000,
            MinSalePrice: 19000,
            DiscountPercent: 5m,
            MinLevel: 2,
            MaxLevel: 50,
            ReorderLevel: 5,
            Description: "Samsung M25 Smartphone with Box packaging",
            Barcode: "8901234567890",
            IsActive: true));

        itemDto.Should().NotBeNull();
        itemDto.Name.Should().Be("Samsung M25");
        itemDto.ItemGroupName.Should().Be("Monitors");
        itemDto.UnitName.Should().Be("Pcs");
        itemDto.AlternateUnitName.Should().Be("Box");
        itemDto.ConversionFactor.Should().Be(10m);
        itemDto.OpeningStockQuantity.Should().Be(10);
        itemDto.OpeningStockRate.Amount.Should().Be(18000);
        itemDto.OpeningStockValue.Amount.Should().Be(180000);
        itemDto.SalesPrice!.Value.Amount.Should().Be(20000);
        itemDto.MRP!.Value.Amount.Should().Be(22000);
        itemDto.MinLevel.Should().Be(2);
        itemDto.ReorderLevel.Should().Be(5);

        // 5. Verify Item List
        var itemsList = await _itemService.GetAllItemsAsync(company.Id);
        itemsList.Should().ContainSingle(i => i.Name == "Samsung M25" && i.Code == "SM-M25-BLK");

        // 6. Verify Delete Restrictions (Unit and Group cannot be deleted while referenced)
        var actDeleteUnit = async () => await _unitService.DeleteUnitAsync(company.Id, pcsUnit.Id);
        await actDeleteUnit.Should().ThrowAsync<DomainException>().WithMessage("*referenced by one or more items*");

        var actDeleteGroup = async () => await _groupService.DeleteGroupAsync(company.Id, monitorsGroup.Id);
        await actDeleteGroup.Should().ThrowAsync<DomainException>().WithMessage("*referenced by one or more items*");

        var actDeleteParentGroup = async () => await _groupService.DeleteGroupAsync(company.Id, electronicsGroup.Id);
        await actDeleteParentGroup.Should().ThrowAsync<DomainException>().WithMessage("*has one or more sub-groups*");
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

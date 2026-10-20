using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class ItemRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly ItemGroupRepository _groupRepository;
    private readonly UnitRepository _unitRepository;
    private readonly ItemRepository _itemRepository;

    public ItemRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_ItmTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _groupRepository = new ItemGroupRepository(_connectionFactory);
        _unitRepository = new UnitRepository(_connectionFactory);
        _itemRepository = new ItemRepository(_connectionFactory);
    }

    private async Task<(Company company, ItemGroup group, Unit unit)> SetupPrerequisitesAsync()
    {
        var company = Company.Create(
            companyName: "Test Company",
            printName: "Test Company",
            shortName: "TC",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));
        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";
        await _companyRepository.AddAsync(company);

        var group = ItemGroup.Create(company.Id, "Electronics");
        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";
        await _groupRepository.AddAsync(group);

        var unit = Unit.Create(company.Id, "Pcs", "Pieces", 0);
        unit.CreatedAt = DateTime.UtcNow;
        unit.CreatedBy = "tester";
        await _unitRepository.AddAsync(unit);

        return (company, group, unit);
    }

    [Fact]
    public async Task AddAndGetById_PersistsItemWithAllFieldsSuccessfully()
    {
        await _initializer.InitializeAsync();
        var (company, group, unit) = await SetupPrerequisitesAsync();

        var altUnit = Unit.Create(company.Id, "Box", "Box", 0);
        altUnit.CreatedAt = DateTime.UtcNow;
        altUnit.CreatedBy = "tester";
        await _unitRepository.AddAsync(altUnit);

        var item = Item.Create(
            company.Id,
            group.Id,
            unit.Id,
            "Samsung M25",
            printName: "Samsung Galaxy M25",
            code: "SM-M25",
            alternateUnitId: altUnit.Id,
            conversionFactor: 10m,
            openingStockQuantity: 10,
            openingStockRate: Money.FromDecimal(18000),
            openingStockValue: Money.FromDecimal(180000),
            salesPrice: Money.FromDecimal(20000),
            purchasePrice: Money.FromDecimal(17500),
            mrp: Money.FromDecimal(22000),
            minSalePrice: Money.FromDecimal(19000),
            discountPercent: 5.5m,
            minLevel: 2,
            maxLevel: 20,
            reorderLevel: 5,
            description: "Samsung Smartphone",
            barcode: "8901234567890",
            isActive: true);
        item.CreatedAt = DateTime.UtcNow;
        item.CreatedBy = "tester";

        await _itemRepository.AddAsync(item);

        var retrieved = await _itemRepository.GetByIdAsync(company.Id, item.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(item.Id);
        retrieved.CompanyId.Should().Be(company.Id);
        retrieved.ItemGroupId.Should().Be(group.Id);
        retrieved.UnitId.Should().Be(unit.Id);
        retrieved.AlternateUnitId.Should().Be(altUnit.Id);
        retrieved.ConversionFactor.Should().Be(10m);
        retrieved.Name.Should().Be("Samsung M25");
        retrieved.PrintName.Should().Be("Samsung Galaxy M25");
        retrieved.Code.Should().Be("SM-M25");
        retrieved.OpeningStockQuantity.Should().Be(10);
        retrieved.OpeningStockRate.Amount.Should().Be(18000);
        retrieved.OpeningStockValue.Amount.Should().Be(180000);
        retrieved.SalesPrice!.Value.Amount.Should().Be(20000);
        retrieved.PurchasePrice!.Value.Amount.Should().Be(17500);
        retrieved.MRP!.Value.Amount.Should().Be(22000);
        retrieved.MinSalePrice!.Value.Amount.Should().Be(19000);
        retrieved.DiscountPercent.Should().Be(5.5m);
        retrieved.MinLevel.Should().Be(2);
        retrieved.MaxLevel.Should().Be(20);
        retrieved.ReorderLevel.Should().Be(5);
        retrieved.Description.Should().Be("Samsung Smartphone");
        retrieved.Barcode.Should().Be("8901234567890");
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Update_PersistsModifiedItem()
    {
        await _initializer.InitializeAsync();
        var (company, group, unit) = await SetupPrerequisitesAsync();

        var item = Item.Create(
            company.Id,
            group.Id,
            unit.Id,
            "Old Name",
            openingStockQuantity: 5,
            openingStockRate: Money.FromDecimal(100),
            openingStockValue: Money.FromDecimal(500));
        item.CreatedAt = DateTime.UtcNow;
        item.CreatedBy = "tester";
        await _itemRepository.AddAsync(item);

        item.Update(
            group.Id,
            unit.Id,
            "New Name",
            "New Print Name",
            "NEW-CODE",
            null,
            null,
            8,
            Money.FromDecimal(120),
            Money.FromDecimal(960),
            Money.FromDecimal(150),
            Money.FromDecimal(110),
            Money.FromDecimal(160),
            Money.FromDecimal(140),
            10m,
            1,
            10,
            3,
            "Updated Desc",
            "NEW-BARCODE",
            true);
        item.ModifiedAt = DateTime.UtcNow;
        item.ModifiedBy = "updater";
        await _itemRepository.UpdateAsync(item);

        var retrieved = await _itemRepository.GetByIdAsync(company.Id, item.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("New Name");
        retrieved.Code.Should().Be("NEW-CODE");
        retrieved.OpeningStockQuantity.Should().Be(8);
        retrieved.OpeningStockRate.Amount.Should().Be(120);
        retrieved.OpeningStockValue.Amount.Should().Be(960);
        retrieved.SalesPrice!.Value.Amount.Should().Be(150);
        retrieved.ModifiedBy.Should().Be("updater");
    }

    [Fact]
    public async Task Delete_RemovesItem()
    {
        await _initializer.InitializeAsync();
        var (company, group, unit) = await SetupPrerequisitesAsync();

        var item = Item.Create(company.Id, group.Id, unit.Id, "Item To Delete");
        item.CreatedAt = DateTime.UtcNow;
        item.CreatedBy = "tester";
        await _itemRepository.AddAsync(item);

        await _itemRepository.DeleteAsync(company.Id, item.Id);

        var retrieved = await _itemRepository.GetByIdAsync(company.Id, item.Id);
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

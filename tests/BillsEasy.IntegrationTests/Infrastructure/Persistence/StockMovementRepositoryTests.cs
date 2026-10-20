using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class StockMovementRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly UnitRepository _unitRepository;
    private readonly ItemGroupRepository _itemGroupRepository;
    private readonly ItemRepository _itemRepository;
    private readonly StockMovementRepository _stockMovementRepository;

    private Guid _companyId;
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public StockMovementRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_StockRepoTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _unitRepository = new UnitRepository(_connectionFactory);
        _itemGroupRepository = new ItemGroupRepository(_connectionFactory);
        _itemRepository = new ItemRepository(_connectionFactory);
        _stockMovementRepository = new StockMovementRepository(_connectionFactory);
    }

    private async Task SeedPrerequisitesAsync()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Test Company",
            printName: "Test Company",
            shortName: null,
            stateName: "Delhi",
            stateCode: "07",
            financialYearBeginning: new DateOnly(2026, 4, 1),
            booksBeginningFrom: new DateOnly(2026, 4, 1));
        await _companyRepository.AddAsync(company);
        _companyId = company.Id;

        var unit = new Unit(_unitId, company.Id, "Pcs", "Pieces", 0);
        await _unitRepository.AddAsync(unit);

        var group = new ItemGroup(_itemGroupId, company.Id, "Electronics");
        await _itemGroupRepository.AddAsync(group);

        var item = new Item(_itemId, company.Id, _itemGroupId, _unitId, "Samsung 19 Inch Monitor");
        await _itemRepository.AddAsync(item);
    }

    [Fact]
    public async Task AddRangeAsync_And_GetCurrentQuantityAsync_CalculatesCorrectBalance()
    {
        await SeedPrerequisitesAsync();

        var m1 = new StockMovement(
            id: Guid.NewGuid(),
            companyId: _companyId,
            itemId: _itemId,
            movementDate: new DateOnly(2026, 5, 1),
            movementType: StockMovementType.PurchaseInward,
            sourceDocumentType: "PurchaseInvoice",
            sourceDocumentId: Guid.NewGuid(),
            quantity: 10m,
            unitId: _unitId,
            rate: Money.FromDecimal(5000m),
            materialCentre: "Main Store");

        var m2 = new StockMovement(
            id: Guid.NewGuid(),
            companyId: _companyId,
            itemId: _itemId,
            movementDate: new DateOnly(2026, 5, 5),
            movementType: StockMovementType.PurchaseInward,
            sourceDocumentType: "PurchaseInvoice",
            sourceDocumentId: Guid.NewGuid(),
            quantity: 5m,
            unitId: _unitId,
            rate: Money.FromDecimal(5200m),
            materialCentre: "Main Store");

        await _stockMovementRepository.AddRangeAsync(new List<StockMovement> { m1, m2 });

        var currentQty = await _stockMovementRepository.GetCurrentQuantityAsync(_companyId, _itemId, "Main Store");
        currentQty.Should().Be(15m);

        var movements = await _stockMovementRepository.GetByItemIdAsync(_companyId, _itemId);
        movements.Should().HaveCount(2);
        movements.Select(m => m.Quantity).Should().ContainInOrder(10m, 5m);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

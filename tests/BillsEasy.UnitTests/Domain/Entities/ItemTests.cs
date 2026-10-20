using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class ItemTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _altUnitId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsItem()
    {
        var item = Item.Create(
            _companyId,
            _itemGroupId,
            _unitId,
            "Samsung M25",
            printName: "Samsung Galaxy M25",
            code: "SM-M25",
            alternateUnitId: _altUnitId,
            conversionFactor: 10m,
            openingStockQuantity: 10,
            openingStockRate: Money.FromDecimal(18000),
            openingStockValue: Money.FromDecimal(180000),
            salesPrice: Money.FromDecimal(20000),
            purchasePrice: Money.FromDecimal(17500),
            mrp: Money.FromDecimal(22000),
            minSalePrice: Money.FromDecimal(19000),
            discountPercent: 5m,
            minLevel: 2,
            maxLevel: 20,
            reorderLevel: 5,
            description: "Samsung smartphone",
            barcode: "8901234567890",
            isActive: true);

        item.Should().NotBeNull();
        item.Id.Should().NotBeEmpty();
        item.CompanyId.Should().Be(_companyId);
        item.ItemGroupId.Should().Be(_itemGroupId);
        item.UnitId.Should().Be(_unitId);
        item.AlternateUnitId.Should().Be(_altUnitId);
        item.ConversionFactor.Should().Be(10m);
        item.Name.Should().Be("Samsung M25");
        item.PrintName.Should().Be("Samsung Galaxy M25");
        item.Code.Should().Be("SM-M25");
        item.OpeningStockQuantity.Should().Be(10);
        item.OpeningStockRate.Amount.Should().Be(18000);
        item.OpeningStockValue.Amount.Should().Be(180000);
        item.SalesPrice!.Value.Amount.Should().Be(20000);
        item.PurchasePrice!.Value.Amount.Should().Be(17500);
        item.MRP!.Value.Amount.Should().Be(22000);
        item.MinSalePrice!.Value.Amount.Should().Be(19000);
        item.DiscountPercent.Should().Be(5m);
        item.MinLevel.Should().Be(2);
        item.MaxLevel.Should().Be(20);
        item.ReorderLevel.Should().Be(5);
        item.Description.Should().Be("Samsung smartphone");
        item.Barcode.Should().Be("8901234567890");
        item.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => Item.Create(_companyId, _itemGroupId, _unitId, invalidName!);

        act.Should().Throw<DomainException>()
            .WithMessage("*name is required*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => Item.Create(Guid.Empty, _itemGroupId, _unitId, "Item 1");

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyGroupId_ThrowsDomainException()
    {
        Action act = () => Item.Create(_companyId, Guid.Empty, _unitId, "Item 1");

        act.Should().Throw<DomainException>()
            .WithMessage("*ItemGroupId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyUnitId_ThrowsDomainException()
    {
        Action act = () => Item.Create(_companyId, _itemGroupId, Guid.Empty, "Item 1");

        act.Should().Throw<DomainException>()
            .WithMessage("*UnitId cannot be empty*");
    }

    [Fact]
    public void Create_WithSameMainAndAlternateUnit_ThrowsDomainException()
    {
        Action act = () => Item.Create(
            _companyId,
            _itemGroupId,
            _unitId,
            "Item 1",
            alternateUnitId: _unitId,
            conversionFactor: 1m);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be the same as main unit*");
    }

    [Fact]
    public void Create_WithAlternateUnitAndMissingOrZeroConversionFactor_ThrowsDomainException()
    {
        Action act1 = () => Item.Create(
            _companyId,
            _itemGroupId,
            _unitId,
            "Item 1",
            alternateUnitId: _altUnitId,
            conversionFactor: null);

        act1.Should().Throw<DomainException>()
            .WithMessage("*greater than zero*");

        Action act2 = () => Item.Create(
            _companyId,
            _itemGroupId,
            _unitId,
            "Item 1",
            alternateUnitId: _altUnitId,
            conversionFactor: 0m);

        act2.Should().Throw<DomainException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void Create_WithNegativeOpeningStock_ThrowsDomainException()
    {
        Action act1 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", openingStockQuantity: -1);
        act1.Should().Throw<DomainException>().WithMessage("*quantity cannot be negative*");

        Action act2 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", openingStockRate: Money.FromDecimal(-10));
        act2.Should().Throw<DomainException>().WithMessage("*rate cannot be negative*");

        Action act3 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", openingStockValue: Money.FromDecimal(-100));
        act3.Should().Throw<DomainException>().WithMessage("*value cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativePrices_ThrowsDomainException()
    {
        Action act1 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", salesPrice: Money.FromDecimal(-10));
        act1.Should().Throw<DomainException>().WithMessage("*Sales price cannot be negative*");

        Action act2 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", purchasePrice: Money.FromDecimal(-10));
        act2.Should().Throw<DomainException>().WithMessage("*Purchase price cannot be negative*");

        Action act3 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", mrp: Money.FromDecimal(-10));
        act3.Should().Throw<DomainException>().WithMessage("*MRP cannot be negative*");

        Action act4 = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", minSalePrice: Money.FromDecimal(-10));
        act4.Should().Throw<DomainException>().WithMessage("*Minimum sale price cannot be negative*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithInvalidDiscountPercent_ThrowsDomainException(decimal discount)
    {
        Action act = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", discountPercent: discount);

        act.Should().Throw<DomainException>()
            .WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void Create_WithMaxLevelLessThanMinLevel_ThrowsDomainException()
    {
        Action act = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", minLevel: 10, maxLevel: 5);

        act.Should().Throw<DomainException>()
            .WithMessage("*Maximum level cannot be less than minimum level*");
    }

    [Fact]
    public void Create_WithReorderLevelLessThanMinLevel_ThrowsDomainException()
    {
        Action act = () => Item.Create(_companyId, _itemGroupId, _unitId, "Item 1", minLevel: 10, reorderLevel: 5);

        act.Should().Throw<DomainException>()
            .WithMessage("*Reorder level cannot be less than minimum level*");
    }
}

using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Domain entity representing a Stock Item / Product in the inventory master.
/// </summary>
public class Item : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public Guid ItemGroupId { get; private set; }
    public Guid UnitId { get; private set; } // Main Unit
    public Guid? AlternateUnitId { get; private set; } // Optional Alternate Unit
    public decimal? ConversionFactor { get; private set; } // e.g., Main/Alt ratio

    // Identity
    public string Name { get; private set; } = string.Empty;
    public string PrintName { get; private set; } = string.Empty;
    public string? Code { get; private set; }

    // Opening Stock
    public decimal OpeningStockQuantity { get; private set; }
    public Money OpeningStockRate { get; private set; } = Money.Zero();
    public Money OpeningStockValue { get; private set; } = Money.Zero();

    // Pricing
    public Money? SalesPrice { get; private set; }
    public Money? PurchasePrice { get; private set; }
    public Money? MRP { get; private set; }
    public Money? MinSalePrice { get; private set; }
    public decimal? DiscountPercent { get; private set; }

    // Inventory Controls
    public decimal? MinLevel { get; private set; }
    public decimal? MaxLevel { get; private set; }
    public decimal? ReorderLevel { get; private set; }

    // Metadata & Status
    public string? Description { get; private set; }
    public string? Barcode { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Parameterless constructor for persistence/ADO.NET hydration
    private Item() : base()
    {
    }

    public Item(
        Guid id,
        Guid companyId,
        Guid itemGroupId,
        Guid unitId,
        string name,
        string? printName = null,
        string? code = null,
        Guid? alternateUnitId = null,
        decimal? conversionFactor = null,
        decimal openingStockQuantity = 0,
        Money? openingStockRate = null,
        Money? openingStockValue = null,
        Money? salesPrice = null,
        Money? purchasePrice = null,
        Money? mrp = null,
        Money? minSalePrice = null,
        decimal? discountPercent = null,
        decimal? minLevel = null,
        decimal? maxLevel = null,
        decimal? reorderLevel = null,
        string? description = null,
        string? barcode = null,
        bool isActive = true) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");
        if (itemGroupId == Guid.Empty)
            throw new DomainException("ItemGroupId cannot be empty.");
        if (unitId == Guid.Empty)
            throw new DomainException("UnitId cannot be empty.");

        CompanyId = companyId;
        ItemGroupId = itemGroupId;
        UnitId = unitId;

        ValidateAndSetName(name);
        ValidateAndSetPrintName(printName);
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        ValidateAndSetUnits(unitId, alternateUnitId, conversionFactor);
        ValidateAndSetOpeningStock(openingStockQuantity, openingStockRate ?? Money.Zero(), openingStockValue ?? Money.Zero());
        ValidateAndSetPricing(salesPrice, purchasePrice, mrp, minSalePrice, discountPercent);
        ValidateAndSetInventoryControls(minLevel, maxLevel, reorderLevel);

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        IsActive = isActive;
    }

    public static Item Create(
        Guid companyId,
        Guid itemGroupId,
        Guid unitId,
        string name,
        string? printName = null,
        string? code = null,
        Guid? alternateUnitId = null,
        decimal? conversionFactor = null,
        decimal openingStockQuantity = 0,
        Money? openingStockRate = null,
        Money? openingStockValue = null,
        Money? salesPrice = null,
        Money? purchasePrice = null,
        Money? mrp = null,
        Money? minSalePrice = null,
        decimal? discountPercent = null,
        decimal? minLevel = null,
        decimal? maxLevel = null,
        decimal? reorderLevel = null,
        string? description = null,
        string? barcode = null,
        bool isActive = true)
    {
        return new Item(
            Guid.NewGuid(),
            companyId,
            itemGroupId,
            unitId,
            name,
            printName,
            code,
            alternateUnitId,
            conversionFactor,
            openingStockQuantity,
            openingStockRate,
            openingStockValue,
            salesPrice,
            purchasePrice,
            mrp,
            minSalePrice,
            discountPercent,
            minLevel,
            maxLevel,
            reorderLevel,
            description,
            barcode,
            isActive);
    }

    public void Update(
        Guid itemGroupId,
        Guid unitId,
        string name,
        string? printName,
        string? code,
        Guid? alternateUnitId,
        decimal? conversionFactor,
        decimal openingStockQuantity,
        Money openingStockRate,
        Money openingStockValue,
        Money? salesPrice,
        Money? purchasePrice,
        Money? mrp,
        Money? minSalePrice,
        decimal? discountPercent,
        decimal? minLevel,
        decimal? maxLevel,
        decimal? reorderLevel,
        string? description,
        string? barcode,
        bool isActive)
    {
        if (itemGroupId == Guid.Empty)
            throw new DomainException("ItemGroupId cannot be empty.");
        if (unitId == Guid.Empty)
            throw new DomainException("UnitId cannot be empty.");

        ItemGroupId = itemGroupId;
        UnitId = unitId;

        ValidateAndSetName(name);
        ValidateAndSetPrintName(printName);
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        ValidateAndSetUnits(unitId, alternateUnitId, conversionFactor);
        ValidateAndSetOpeningStock(openingStockQuantity, openingStockRate, openingStockValue);
        ValidateAndSetPricing(salesPrice, purchasePrice, mrp, minSalePrice, discountPercent);
        ValidateAndSetInventoryControls(minLevel, maxLevel, reorderLevel);

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        IsActive = isActive;
    }

    private void ValidateAndSetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Item name is required.");

        Name = name.Trim();
    }

    private void ValidateAndSetPrintName(string? printName)
    {
        if (string.IsNullOrWhiteSpace(printName))
        {
            PrintName = Name;
        }
        else
        {
            PrintName = printName.Trim();
        }
    }

    private void ValidateAndSetUnits(Guid unitId, Guid? alternateUnitId, decimal? conversionFactor)
    {
        if (alternateUnitId.HasValue)
        {
            if (alternateUnitId.Value == unitId)
                throw new DomainException("Alternate unit cannot be the same as main unit.");

            if (!conversionFactor.HasValue || conversionFactor.Value <= 0)
                throw new DomainException("Conversion factor must be greater than zero when an alternate unit is specified.");

            AlternateUnitId = alternateUnitId;
            ConversionFactor = conversionFactor;
        }
        else
        {
            AlternateUnitId = null;
            ConversionFactor = null;
        }
    }

    private void ValidateAndSetOpeningStock(decimal quantity, Money rate, Money value)
    {
        if (quantity < 0)
            throw new DomainException("Opening stock quantity cannot be negative.");
        if (rate.Amount < 0)
            throw new DomainException("Opening stock rate cannot be negative.");
        if (value.Amount < 0)
            throw new DomainException("Opening stock value cannot be negative.");

        OpeningStockQuantity = quantity;
        OpeningStockRate = rate;
        OpeningStockValue = value;
    }

    private void ValidateAndSetPricing(
        Money? salesPrice,
        Money? purchasePrice,
        Money? mrp,
        Money? minSalePrice,
        decimal? discountPercent)
    {
        if (salesPrice.HasValue && salesPrice.Value.Amount < 0)
            throw new DomainException("Sales price cannot be negative.");
        if (purchasePrice.HasValue && purchasePrice.Value.Amount < 0)
            throw new DomainException("Purchase price cannot be negative.");
        if (mrp.HasValue && mrp.Value.Amount < 0)
            throw new DomainException("MRP cannot be negative.");
        if (minSalePrice.HasValue && minSalePrice.Value.Amount < 0)
            throw new DomainException("Minimum sale price cannot be negative.");

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            throw new DomainException("Discount percent must be between 0 and 100.");

        SalesPrice = salesPrice;
        PurchasePrice = purchasePrice;
        MRP = mrp;
        MinSalePrice = minSalePrice;
        DiscountPercent = discountPercent;
    }

    private void ValidateAndSetInventoryControls(decimal? minLevel, decimal? maxLevel, decimal? reorderLevel)
    {
        if (minLevel.HasValue && minLevel.Value < 0)
            throw new DomainException("Minimum level cannot be negative.");
        if (maxLevel.HasValue && maxLevel.Value < 0)
            throw new DomainException("Maximum level cannot be negative.");
        if (reorderLevel.HasValue && reorderLevel.Value < 0)
            throw new DomainException("Reorder level cannot be negative.");

        if (minLevel.HasValue && maxLevel.HasValue && maxLevel.Value < minLevel.Value)
            throw new DomainException("Maximum level cannot be less than minimum level.");

        if (minLevel.HasValue && reorderLevel.HasValue && reorderLevel.Value < minLevel.Value)
            throw new DomainException("Reorder level cannot be less than minimum level.");

        MinLevel = minLevel;
        MaxLevel = maxLevel;
        ReorderLevel = reorderLevel;
    }
}

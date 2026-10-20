using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Physical stock movement record.
/// In Phase 1.8, records inward quantity additions (+Qty at "Main Store") with historical acquisition rate.
/// Strictly non-valuation (FIFO/Weighted Average valuation is deferred).
/// </summary>
public class StockMovement : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public Guid ItemId { get; private set; }
    public DateOnly MovementDate { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public string SourceDocumentType { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid UnitId { get; private set; }
    public Money Rate { get; private set; }
    public Money TotalValue { get; private set; }
    public string MaterialCentre { get; private set; }
    public bool IsActive { get; private set; }

    private StockMovement() : base()
    {
        SourceDocumentType = string.Empty;
        MaterialCentre = "Main Store";
        Rate = Money.Zero();
        TotalValue = Money.Zero();
        IsActive = true;
    }

    public StockMovement(
        Guid id,
        Guid companyId,
        Guid itemId,
        DateOnly movementDate,
        StockMovementType movementType,
        string sourceDocumentType,
        Guid sourceDocumentId,
        decimal quantity,
        Guid unitId,
        Money rate,
        string materialCentre = "Main Store",
        bool isActive = true) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (itemId == Guid.Empty)
            throw new DomainException("ItemId cannot be empty.");

        if (unitId == Guid.Empty)
            throw new DomainException("UnitId cannot be empty.");

        if (sourceDocumentId == Guid.Empty)
            throw new DomainException("SourceDocumentId cannot be empty.");

        if (string.IsNullOrWhiteSpace(sourceDocumentType))
            throw new DomainException("SourceDocumentType cannot be empty.");

        if (quantity == 0)
            throw new DomainException("Quantity cannot be zero.");

        if (rate.Amount < 0)
            throw new DomainException("Rate cannot be negative.");

        var trimmedCentre = string.IsNullOrWhiteSpace(materialCentre) ? "Main Store" : materialCentre.Trim();

        CompanyId = companyId;
        ItemId = itemId;
        MovementDate = movementDate;
        MovementType = movementType;
        SourceDocumentType = sourceDocumentType.Trim();
        SourceDocumentId = sourceDocumentId;
        Quantity = quantity;
        UnitId = unitId;
        Rate = rate;
        TotalValue = Money.FromDecimal(Math.Abs(quantity) * rate.Amount);
        MaterialCentre = trimmedCentre;
        IsActive = isActive;
    }

    public void Deactivate(string modifiedBy, DateTime modifiedAtUtc)
    {
        IsActive = false;
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAtUtc;
    }
}

using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Individual line item in a commercial purchase invoice.
/// Class 8 observed fields: S.N., Item, Qty, Unit, Price, Amount.
/// </summary>
public class PurchaseInvoiceItemLine : Entity<Guid>
{
    public Guid PurchaseInvoiceId { get; private set; }
    public int SequenceNumber { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid UnitId { get; private set; }
    public Money Price { get; private set; }
    public Money Amount { get; private set; }

    private PurchaseInvoiceItemLine() : base() { }

    public PurchaseInvoiceItemLine(
        Guid id,
        Guid purchaseInvoiceId,
        int sequenceNumber,
        Guid itemId,
        decimal quantity,
        Guid unitId,
        Money price) : base(id)
    {
        if (purchaseInvoiceId == Guid.Empty)
            throw new DomainException("PurchaseInvoiceId cannot be empty.");

        if (sequenceNumber <= 0)
            throw new DomainException("SequenceNumber must be greater than zero.");

        if (itemId == Guid.Empty)
            throw new DomainException("ItemId cannot be empty.");

        if (unitId == Guid.Empty)
            throw new DomainException("UnitId cannot be empty.");

        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        if (price.Amount < 0)
            throw new DomainException("Price cannot be negative.");

        PurchaseInvoiceId = purchaseInvoiceId;
        SequenceNumber = sequenceNumber;
        ItemId = itemId;
        Quantity = quantity;
        UnitId = unitId;
        Price = price;
        Amount = Money.FromDecimal(quantity * price.Amount);
    }

    public void UpdateSequenceNumber(int newSequenceNumber)
    {
        if (newSequenceNumber <= 0)
            throw new DomainException("SequenceNumber must be greater than zero.");

        SequenceNumber = newSequenceNumber;
    }
}

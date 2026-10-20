using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Individual bill sundry charge (e.g. Freight & Forwarding Charges, Rounded Off) in a purchase invoice.
/// Class 8 observed fields: S.N., Bill Sundry, @, Amount.
/// </summary>
public class PurchaseInvoiceSundryLine : Entity<Guid>
{
    public Guid PurchaseInvoiceId { get; private set; }
    public int SequenceNumber { get; private set; }
    public Guid LedgerId { get; private set; }
    public decimal? Percentage { get; private set; }
    public Money Amount { get; private set; }

    private PurchaseInvoiceSundryLine() : base() { }

    public PurchaseInvoiceSundryLine(
        Guid id,
        Guid purchaseInvoiceId,
        int sequenceNumber,
        Guid ledgerId,
        Money amount,
        decimal? percentage = null) : base(id)
    {
        if (purchaseInvoiceId == Guid.Empty)
            throw new DomainException("PurchaseInvoiceId cannot be empty.");

        if (sequenceNumber <= 0)
            throw new DomainException("SequenceNumber must be greater than zero.");

        if (ledgerId == Guid.Empty)
            throw new DomainException("LedgerId cannot be empty.");

        if (amount.Amount <= 0)
            throw new DomainException("Sundry amount must be greater than zero.");

        PurchaseInvoiceId = purchaseInvoiceId;
        SequenceNumber = sequenceNumber;
        LedgerId = ledgerId;
        Amount = amount;
        Percentage = percentage;
    }

    public void UpdateSequenceNumber(int newSequenceNumber)
    {
        if (newSequenceNumber <= 0)
            throw new DomainException("SequenceNumber must be greater than zero.");

        SequenceNumber = newSequenceNumber;
    }
}

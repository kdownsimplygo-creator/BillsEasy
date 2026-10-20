using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Represents an individual debit or credit line within a double-entry voucher.
/// </summary>
public class VoucherLine : Entity<Guid>
{
    public Guid VoucherId { get; private set; }
    public Guid LedgerId { get; private set; }
    public int SequenceNumber { get; private set; }
    public DebitCreditType Type { get; private set; }
    public Money Amount { get; private set; }
    public string? Narration { get; private set; }

    // Parameterless constructor for persistence/hydration
    private VoucherLine() : base()
    {
    }

    public VoucherLine(
        Guid id,
        Guid voucherId,
        Guid ledgerId,
        int sequenceNumber,
        DebitCreditType type,
        Money amount,
        string? narration = null) : base(id)
    {
        if (voucherId == Guid.Empty)
            throw new DomainException("VoucherId cannot be empty.");

        if (ledgerId == Guid.Empty)
            throw new DomainException("LedgerId cannot be empty.");

        if (sequenceNumber <= 0)
            throw new DomainException("SequenceNumber must be greater than zero.");

        if (amount.Amount <= 0)
            throw new DomainException("VoucherLine amount must be greater than zero.");

        if (narration != null && narration.Length > 500)
            throw new DomainException("Line narration cannot exceed 500 characters.");

        VoucherId = voucherId;
        LedgerId = ledgerId;
        SequenceNumber = sequenceNumber;
        Type = type;
        Amount = amount;
        Narration = narration?.Trim();
    }

    internal void UpdateSequenceNumber(int newSequenceNumber)
    {
        if (newSequenceNumber <= 0)
            throw new DomainException("SequenceNumber must be greater than zero.");

        SequenceNumber = newSequenceNumber;
    }
}

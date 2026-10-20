using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Represents an individual commercial invoice or opening balance bill reference.
/// Tracks commercial debt commitments and receivable claims partitioned by Company and Ledger.
/// </summary>
public class BillReference : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public Guid LedgerId { get; private set; }
    public string ReferenceNumber { get; private set; } = string.Empty;
    public BillReferenceType ReferenceType { get; private set; }
    public BillReferenceStatus Status { get; private set; } = BillReferenceStatus.Active;
    public DateOnly BillDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Money OriginalAmount { get; private set; }
    public DebitCreditType EntryType { get; private set; }
    public Guid? SourceVoucherLineId { get; private set; }

    // Parameterless constructor for persistence / hydration
    private BillReference() : base()
    {
    }

    public BillReference(
        Guid id,
        Guid companyId,
        Guid ledgerId,
        string referenceNumber,
        BillReferenceType referenceType,
        DateOnly billDate,
        Money originalAmount,
        DebitCreditType entryType,
        DateOnly? dueDate = null,
        BillReferenceStatus status = BillReferenceStatus.Active,
        Guid? sourceVoucherLineId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainException("Id cannot be empty.");

        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (ledgerId == Guid.Empty)
            throw new DomainException("LedgerId cannot be empty.");

        if (string.IsNullOrWhiteSpace(referenceNumber))
            throw new DomainException("ReferenceNumber cannot be empty.");

        var trimmedRef = referenceNumber.Trim();
        if (trimmedRef.Length > 50)
            throw new DomainException("ReferenceNumber cannot exceed 50 characters.");

        if (originalAmount.Amount <= 0)
            throw new DomainException("OriginalAmount must be greater than zero.");

        if (!Enum.IsDefined(typeof(BillReferenceType), referenceType))
            throw new DomainException($"Invalid BillReferenceType: {referenceType}.");

        if (!Enum.IsDefined(typeof(BillReferenceStatus), status))
            throw new DomainException($"Invalid BillReferenceStatus: {status}.");

        if (!Enum.IsDefined(typeof(DebitCreditType), entryType))
            throw new DomainException($"Invalid EntryType: {entryType}.");

        if (sourceVoucherLineId.HasValue && sourceVoucherLineId.Value == Guid.Empty)
            throw new DomainException("SourceVoucherLineId cannot be empty.");

        CompanyId = companyId;
        LedgerId = ledgerId;
        ReferenceNumber = trimmedRef;
        ReferenceType = referenceType;
        BillDate = billDate;
        DueDate = dueDate;
        OriginalAmount = originalAmount;
        EntryType = entryType;
        Status = status;
        SourceVoucherLineId = sourceVoucherLineId;
    }

    public static BillReference Create(
        Guid companyId,
        Guid ledgerId,
        string referenceNumber,
        BillReferenceType referenceType,
        DateOnly billDate,
        Money originalAmount,
        DebitCreditType entryType,
        DateOnly? dueDate = null,
        Guid? sourceVoucherLineId = null)
    {
        return new BillReference(
            Guid.NewGuid(),
            companyId,
            ledgerId,
            referenceNumber,
            referenceType,
            billDate,
            originalAmount,
            entryType,
            dueDate,
            BillReferenceStatus.Active,
            sourceVoucherLineId);
    }

    public void Cancel(string? cancelledBy = null, DateTime? cancelledAt = null)
    {
        if (Status == BillReferenceStatus.Cancelled)
            throw new DomainException("BillReference is already cancelled.");

        Status = BillReferenceStatus.Cancelled;
        ModifiedAt = cancelledAt ?? DateTime.UtcNow;
        ModifiedBy = cancelledBy;
    }
}

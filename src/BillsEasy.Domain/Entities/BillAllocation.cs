using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Represents the discrete settlement record between a settlement VoucherLine and an existing BillReference.
/// Enforces INTRINSIC invariants only (identity, positive paise amounts).
/// State-dependent business rules (outstanding limits, cross-ledger/cross-company matching)
/// are strictly enforced at the Application / Domain service layer.
/// </summary>
public class BillAllocation : Entity<Guid>
{
    public Guid CompanyId { get; private set; }
    public Guid VoucherLineId { get; private set; }
    public Guid BillReferenceId { get; private set; }
    public Money AllocatedAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;

    // Parameterless constructor for persistence / hydration
    private BillAllocation() : base()
    {
    }

    public BillAllocation(
        Guid id,
        Guid companyId,
        Guid voucherLineId,
        Guid billReferenceId,
        Money allocatedAmount,
        DateTime createdAt,
        string createdBy) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainException("Id cannot be empty.");

        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (voucherLineId == Guid.Empty)
            throw new DomainException("VoucherLineId cannot be empty.");

        if (billReferenceId == Guid.Empty)
            throw new DomainException("BillReferenceId cannot be empty.");

        if (allocatedAmount.Amount <= 0)
            throw new DomainException("AllocatedAmount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new DomainException("CreatedBy cannot be empty.");

        CompanyId = companyId;
        VoucherLineId = voucherLineId;
        BillReferenceId = billReferenceId;
        AllocatedAmount = allocatedAmount;
        CreatedAt = createdAt;
        CreatedBy = createdBy.Trim();
    }

    public static BillAllocation Create(
        Guid companyId,
        Guid voucherLineId,
        Guid billReferenceId,
        Money allocatedAmount,
        string createdBy,
        DateTime? createdAt = null)
    {
        return new BillAllocation(
            Guid.NewGuid(),
            companyId,
            voucherLineId,
            billReferenceId,
            allocatedAmount,
            createdAt ?? DateTime.UtcNow,
            createdBy);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Authoritative aggregate root representing a double-entry transaction voucher.
/// </summary>
public class Voucher : AuditableEntity<Guid>
{
    private readonly List<VoucherLine> _lines = new();
    private Money? _persistedTotalAmount;

    public Guid CompanyId { get; private set; }
    public Guid FinancialYearId { get; private set; }
    public string VoucherNumber { get; private set; } = string.Empty;
    public VoucherType Type { get; private set; }
    public VoucherStatus Status { get; private set; } = VoucherStatus.Draft;
    public DateOnly VoucherDate { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Narration { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public string? PostedBy { get; private set; }

    public IReadOnlyList<VoucherLine> Lines => _lines.AsReadOnly();

    public Money TotalDebit => new Money(_lines.Where(l => l.Type == DebitCreditType.Debit).Sum(l => l.Amount.Amount));
    public Money TotalCredit => new Money(_lines.Where(l => l.Type == DebitCreditType.Credit).Sum(l => l.Amount.Amount));

    public bool IsBalanced => _lines.Count >= 2 && TotalDebit.Amount > 0 && TotalDebit == TotalCredit;

    public Money TotalAmount
    {
        get
        {
            if (_lines.Count > 0 && TotalDebit == TotalCredit && TotalDebit.Amount > 0)
                return TotalDebit;

            return _persistedTotalAmount ?? TotalDebit;
        }
    }

    // Parameterless constructor for persistence/ORM hydration
    private Voucher() : base()
    {
    }

    public Voucher(
        Guid id,
        Guid companyId,
        Guid financialYearId,
        string voucherNumber,
        VoucherType type,
        DateOnly voucherDate,
        VoucherStatus status = VoucherStatus.Draft,
        string? referenceNumber = null,
        string? narration = null,
        DateTime? postedAt = null,
        string? postedBy = null,
        Money? totalAmount = null) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (financialYearId == Guid.Empty)
            throw new DomainException("FinancialYearId cannot be empty.");

        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new DomainException("VoucherNumber cannot be empty.");

        var trimmedNumber = voucherNumber.Trim();
        if (trimmedNumber.Length > 50)
            throw new DomainException("VoucherNumber cannot exceed 50 characters.");

        if (referenceNumber != null && referenceNumber.Trim().Length > 50)
            throw new DomainException("ReferenceNumber cannot exceed 50 characters.");

        if (narration != null && narration.Trim().Length > 1000)
            throw new DomainException("Narration cannot exceed 1000 characters.");

        CompanyId = companyId;
        FinancialYearId = financialYearId;
        VoucherNumber = trimmedNumber;
        Type = type;
        Status = status;
        VoucherDate = voucherDate;
        ReferenceNumber = referenceNumber?.Trim();
        Narration = narration?.Trim();
        PostedAt = postedAt;
        PostedBy = postedBy;
        _persistedTotalAmount = totalAmount;
    }

    public static Voucher Create(
        Guid companyId,
        Guid financialYearId,
        string voucherNumber,
        VoucherType type,
        DateOnly voucherDate,
        string? referenceNumber = null,
        string? narration = null)
    {
        return new Voucher(
            Guid.NewGuid(),
            companyId,
            financialYearId,
            voucherNumber,
            type,
            voucherDate,
            VoucherStatus.Draft,
            referenceNumber,
            narration);
    }

    public VoucherLine AddLine(Guid ledgerId, DebitCreditType type, Money amount, string? narration = null)
    {
        if (Status != VoucherStatus.Draft)
            throw new DomainException("Cannot modify lines of a voucher that is not in Draft status.");

        var sequenceNumber = _lines.Count + 1;
        var line = new VoucherLine(Guid.NewGuid(), Id, ledgerId, sequenceNumber, type, amount, narration);
        _lines.Add(line);
        return line;
    }

    public void RemoveLine(int sequenceNumber)
    {
        if (Status != VoucherStatus.Draft)
            throw new DomainException("Cannot modify lines of a voucher that is not in Draft status.");

        var line = _lines.FirstOrDefault(l => l.SequenceNumber == sequenceNumber);
        if (line == null)
            throw new DomainException($"Voucher line with sequence number {sequenceNumber} was not found.");

        _lines.Remove(line);

        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].UpdateSequenceNumber(i + 1);
        }
    }

    public void ClearLines()
    {
        if (Status != VoucherStatus.Draft)
            throw new DomainException("Cannot modify lines of a voucher that is not in Draft status.");

        _lines.Clear();
    }

    public void LoadLines(IEnumerable<VoucherLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines.OrderBy(l => l.SequenceNumber));
    }

    public IReadOnlyList<string> GetValidationErrors(FinancialYear? financialYear = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(VoucherNumber))
            errors.Add("VoucherNumber cannot be empty.");

        if (_lines.Count < 2)
            errors.Add("Voucher must have at least 2 lines (at least one debit and one credit).");

        var hasDebit = _lines.Any(l => l.Type == DebitCreditType.Debit);
        var hasCredit = _lines.Any(l => l.Type == DebitCreditType.Credit);

        if (!hasDebit)
            errors.Add("Voucher must contain at least one Debit line.");

        if (!hasCredit)
            errors.Add("Voucher must contain at least one Credit line.");

        if (hasDebit && hasCredit && TotalDebit != TotalCredit)
            errors.Add($"Total Debit ({TotalDebit.Amount:F2}) must equal Total Credit ({TotalCredit.Amount:F2}).");

        if (TotalDebit.Amount <= 0)
            errors.Add("Voucher total amount must be greater than zero.");

        if (financialYear != null)
        {
            if (financialYear.CompanyId != CompanyId)
                errors.Add("Voucher CompanyId does not match FinancialYear CompanyId.");

            if (financialYear.IsLocked)
                errors.Add($"Financial year '{financialYear.YearName}' is locked. Posting is prohibited.");

            if (!financialYear.Contains(VoucherDate))
                errors.Add($"Voucher date {VoucherDate:yyyy-MM-dd} is outside financial year '{financialYear.YearName}' ({financialYear.StartDate:yyyy-MM-dd} to {financialYear.EndDate:yyyy-MM-dd}).");
        }

        return errors;
    }

    public void Validate(FinancialYear? financialYear = null)
    {
        var errors = GetValidationErrors(financialYear);
        if (errors.Count > 0)
            throw new DomainException(string.Join(" ", errors));
    }

    public void Post(FinancialYear financialYear, string? postedBy = null, DateTime? postedAt = null)
    {
        if (Status == VoucherStatus.Posted)
            throw new DomainException("Voucher is already posted.");

        if (Status == VoucherStatus.Cancelled)
            throw new DomainException("Cannot post a cancelled voucher.");

        ArgumentNullException.ThrowIfNull(financialYear);

        Validate(financialYear);

        Status = VoucherStatus.Posted;
        PostedAt = postedAt ?? DateTime.UtcNow;
        PostedBy = postedBy;
    }

    public void Cancel(string? cancelledBy = null)
    {
        if (Status == VoucherStatus.Cancelled)
            throw new DomainException("Voucher is already cancelled.");

        Status = VoucherStatus.Cancelled;
        ModifiedAt = DateTime.UtcNow;
        ModifiedBy = cancelledBy;
    }
}

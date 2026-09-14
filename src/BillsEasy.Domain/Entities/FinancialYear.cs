using System;
using BillsEasy.Domain.Common;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Represents an accounting period boundary for a Company.
/// </summary>
public class FinancialYear : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public string YearName { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }

    // Parameterless constructor for persistence/serialization
    private FinancialYear() : base()
    {
    }

    public FinancialYear(
        Guid id,
        Guid companyId,
        string yearName,
        DateOnly startDate,
        DateOnly endDate,
        bool isActive = false,
        bool isLocked = false) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(yearName))
            throw new DomainException("Financial year name cannot be empty.");

        if (startDate >= endDate)
            throw new DomainException($"Financial year StartDate ({startDate:yyyy-MM-dd}) must precede EndDate ({endDate:yyyy-MM-dd}).");

        CompanyId = companyId;
        YearName = yearName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
        IsLocked = isLocked;
    }

    public static FinancialYear Create(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        string? customName = null,
        bool isActive = false)
    {
        var name = customName ?? $"{startDate.Year}-{endDate.Year}";
        return new FinancialYear(Guid.NewGuid(), companyId, name, startDate, endDate, isActive);
    }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public bool OverlapsWith(DateOnly start, DateOnly end)
    {
        return StartDate <= end && end >= start && start <= EndDate;
    }

    public bool OverlapsWith(FinancialYear other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        return OverlapsWith(other.StartDate, other.EndDate);
    }

    public void SetActive(bool isActive) => IsActive = isActive;
    public void SetLocked(bool isLocked) => IsLocked = isLocked;
}

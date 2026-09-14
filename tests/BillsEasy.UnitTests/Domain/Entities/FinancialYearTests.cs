using System;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class FinancialYearTests
{
    [Fact]
    public void Create_WithValidDates_SetsPropertiesCorrectly()
    {
        var companyId = Guid.NewGuid();
        var start = new DateOnly(2024, 4, 1);
        var end = new DateOnly(2025, 3, 31);

        var fy = FinancialYear.Create(companyId, start, end, "FY 2024-25", isActive: true);

        fy.CompanyId.Should().Be(companyId);
        fy.StartDate.Should().Be(start);
        fy.EndDate.Should().Be(end);
        fy.YearName.Should().Be("FY 2024-25");
        fy.IsActive.Should().BeTrue();
        fy.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenStartDateEqualsOrAfterEndDate_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        var start = new DateOnly(2024, 4, 1);
        var invalidEnd = new DateOnly(2024, 4, 1);

        Action act = () => new FinancialYear(Guid.NewGuid(), companyId, "2024", start, invalidEnd);

        act.Should().Throw<DomainException>()
            .WithMessage("*StartDate*must precede EndDate*");
    }

    [Fact]
    public void Contains_ReturnsTrueForDatesWithinRange()
    {
        var fy = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2024, 4, 1), new DateOnly(2025, 3, 31));

        fy.Contains(new DateOnly(2024, 4, 1)).Should().BeTrue();
        fy.Contains(new DateOnly(2024, 10, 15)).Should().BeTrue();
        fy.Contains(new DateOnly(2025, 3, 31)).Should().BeTrue();
        fy.Contains(new DateOnly(2024, 3, 31)).Should().BeFalse();
        fy.Contains(new DateOnly(2025, 4, 1)).Should().BeFalse();
    }

    [Fact]
    public void OverlapsWith_ReturnsTrueForOverlappingPeriods()
    {
        var fy1 = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2024, 4, 1), new DateOnly(2025, 3, 31));
        var overlapping = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2024, 10, 1), new DateOnly(2025, 9, 30));
        var nonOverlapping = FinancialYear.Create(Guid.NewGuid(), new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31));

        fy1.OverlapsWith(overlapping).Should().BeTrue();
        fy1.OverlapsWith(nonOverlapping).Should().BeFalse();
    }

    [Fact]
    public void AddFinancialYear_WhenOverlapsWithExistingYear_ThrowsDomainException()
    {
        var company = Company.Create("Test Co", "Test", null, "Gujarat", "24", new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));

        var overlappingFy = FinancialYear.Create(company.Id, new DateOnly(2024, 6, 1), new DateOnly(2025, 5, 31), "2024-2025-Overlap");

        Action act = () => company.AddFinancialYear(overlappingFy);

        act.Should().Throw<DomainException>()
            .WithMessage("*overlaps with existing Financial Year*");
    }

    [Fact]
    public void AddFinancialYear_WhenValidContiguousPeriod_AddsSuccessfully()
    {
        var company = Company.Create("Test Co", "Test", null, "Gujarat", "24", new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));

        var nextFy = FinancialYear.Create(company.Id, new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31), "2025-2026", isActive: true);

        company.AddFinancialYear(nextFy);

        company.FinancialYears.Should().HaveCount(2);
        company.FinancialYears.First().IsActive.Should().BeFalse();
        company.FinancialYears.Last().IsActive.Should().BeTrue();
    }
}

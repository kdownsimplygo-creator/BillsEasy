using System;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class CompanyTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesCompanyAndInitialFinancialYear()
    {
        var fyStart = new DateOnly(2024, 4, 1);
        var booksStart = new DateOnly(2024, 4, 1);

        var company = Company.Create(
            companyName: "Ramesh Trading Co",
            printName: "Ramesh Trading Company Pvt Ltd",
            shortName: "RTC",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: fyStart,
            booksBeginningFrom: booksStart,
            gstin: "24AAACR1234A1Z5",
            gstRegistrationType: GSTRegistrationType.Regular);

        company.Id.Should().NotBeEmpty();
        company.CompanyName.Should().Be("Ramesh Trading Co");
        company.PrintName.Should().Be("Ramesh Trading Company Pvt Ltd");
        company.ShortName.Should().Be("RTC");
        company.StateName.Should().Be("Gujarat");
        company.StateCode.Should().Be("24");
        company.GSTIN.Should().Be("24AAACR1234A1Z5");
        company.CurrencySymbol.Should().Be("₹");
        company.CurrencyFormalName.Should().Be("INR");

        company.FinancialYears.Should().HaveCount(1);
        var initialFy = company.FinancialYears.First();
        initialFy.StartDate.Should().Be(new DateOnly(2024, 4, 1));
        initialFy.EndDate.Should().Be(new DateOnly(2025, 3, 31));
        initialFy.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsDomainException(string? invalidName)
    {
        var fyStart = new DateOnly(2024, 4, 1);

        Action act = () => Company.Create(
            companyName: invalidName!,
            printName: "Print",
            shortName: null,
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: fyStart,
            booksBeginningFrom: fyStart);

        act.Should().Throw<DomainException>()
            .WithMessage("*Company name cannot be empty*");
    }

    [Fact]
    public void Create_WhenBooksBeginningBeforeFinancialYearBeginning_ThrowsDomainException()
    {
        var fyStart = new DateOnly(2024, 4, 1);
        var invalidBooksStart = new DateOnly(2024, 3, 31);

        Action act = () => Company.Create(
            companyName: "Test Co",
            printName: "Test Co",
            shortName: null,
            stateName: "Maharashtra",
            stateCode: "27",
            financialYearBeginning: fyStart,
            booksBeginningFrom: invalidBooksStart);

        act.Should().Throw<DomainException>()
            .WithMessage("*Books beginning date*cannot be earlier than Financial year beginning date*");
    }

    [Theory]
    [InlineData("24AAACR1234A1")] // Too short
    [InlineData("24AAACR1234A1Z567")] // Too long
    [InlineData("INVALIDGSTIN1234")] // Non-matching pattern
    public void Create_WithInvalidGSTIN_ThrowsDomainException(string invalidGstin)
    {
        var fyStart = new DateOnly(2024, 4, 1);

        Action act = () => Company.Create(
            companyName: "Test Co",
            printName: "Test Co",
            shortName: null,
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: fyStart,
            booksBeginningFrom: fyStart,
            gstin: invalidGstin);

        act.Should().Throw<DomainException>()
            .WithMessage("*Invalid GSTIN format*");
    }

    [Fact]
    public void Update_WithValidDetails_UpdatesCompanyProperties()
    {
        var fyStart = new DateOnly(2024, 4, 1);
        var company = Company.Create(
            "Original Name", "Orig Print", "ON", "Gujarat", "24", fyStart, fyStart);

        company.Update("New Name", "New Print", "NN", "Maharashtra", "27", "27AAACR1234A1Z5", GSTRegistrationType.Composition, true);

        company.CompanyName.Should().Be("New Name");
        company.PrintName.Should().Be("New Print");
        company.ShortName.Should().Be("NN");
        company.StateName.Should().Be("Maharashtra");
        company.StateCode.Should().Be("27");
        company.GSTIN.Should().Be("27AAACR1234A1Z5");
        company.GSTRegistrationType.Should().Be(GSTRegistrationType.Composition);
        company.IsSecurityEnabled.Should().BeTrue();
    }
}

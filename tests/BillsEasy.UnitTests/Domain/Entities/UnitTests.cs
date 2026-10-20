using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class UnitTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsUnit()
    {
        var unit = Unit.Create(_companyId, "Pcs", "Pieces", 0);

        unit.Should().NotBeNull();
        unit.Id.Should().NotBeEmpty();
        unit.CompanyId.Should().Be(_companyId);
        unit.Name.Should().Be("Pcs");
        unit.PrintName.Should().Be("Pieces");
        unit.DecimalPlaces.Should().Be(0);
    }

    [Fact]
    public void Create_WithNullPrintName_DefaultsToName()
    {
        var unit = Unit.Create(_companyId, "Box", null, 2);

        unit.Name.Should().Be("Box");
        unit.PrintName.Should().Be("Box");
        unit.DecimalPlaces.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => Unit.Create(_companyId, invalidName!);

        act.Should().Throw<DomainException>()
            .WithMessage("*name is required*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => Unit.Create(Guid.Empty, "Kg");

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Create_WithInvalidDecimalPlaces_ThrowsDomainException(int invalidDecimals)
    {
        Action act = () => Unit.Create(_companyId, "Kg", null, invalidDecimals);

        act.Should().Throw<DomainException>()
            .WithMessage("*between 0 and 4*");
    }

    [Fact]
    public void Update_WithValidValues_UpdatesProperties()
    {
        var unit = Unit.Create(_companyId, "Pcs", "Pieces", 0);
        unit.Update("Box", "Boxes", 2);

        unit.Name.Should().Be("Box");
        unit.PrintName.Should().Be("Boxes");
        unit.DecimalPlaces.Should().Be(2);
    }
}

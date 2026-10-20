using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class PurchaseInvoiceItemLineTests
{
    [Fact]
    public void Constructor_ValidArguments_CalculatesAmountCorrectly()
    {
        var line = new PurchaseInvoiceItemLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            quantity: 5.00m,
            Guid.NewGuid(),
            price: 5500.00m);

        line.Quantity.Should().Be(5.00m);
        line.Price.Amount.Should().Be(5500.00m);
        line.Amount.Amount.Should().Be(27500.00m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5.5)]
    public void Constructor_NonPositiveQuantity_ThrowsDomainException(decimal invalidQty)
    {
        var act = () => new PurchaseInvoiceItemLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            invalidQty,
            Guid.NewGuid(),
            price: 100m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Quantity must be greater than zero*");
    }

    [Fact]
    public void Constructor_NegativePrice_ThrowsDomainException()
    {
        var act = () => new PurchaseInvoiceItemLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            quantity: 1m,
            Guid.NewGuid(),
            price: -10m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Price cannot be negative*");
    }

    [Fact]
    public void Constructor_ZeroPrice_IsAllowed()
    {
        var line = new PurchaseInvoiceItemLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            quantity: 2m,
            Guid.NewGuid(),
            price: 0m);

        line.Price.Amount.Should().Be(0m);
        line.Amount.Amount.Should().Be(0m);
    }

    [Fact]
    public void UpdateSequenceNumber_ValidNumber_UpdatesSuccessfully()
    {
        var line = new PurchaseInvoiceItemLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1m,
            Guid.NewGuid(),
            100m);

        line.UpdateSequenceNumber(3);
        line.SequenceNumber.Should().Be(3);
    }

    [Fact]
    public void UpdateSequenceNumber_ZeroOrNegative_ThrowsDomainException()
    {
        var line = new PurchaseInvoiceItemLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1m,
            Guid.NewGuid(),
            100m);

        var act = () => line.UpdateSequenceNumber(0);
        act.Should().Throw<DomainException>()
            .WithMessage("*SequenceNumber must be greater than zero*");
    }
}

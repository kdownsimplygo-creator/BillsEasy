using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class PurchaseInvoiceSundryLineTests
{
    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var ledgerId = Guid.NewGuid();
        var sundry = new PurchaseInvoiceSundryLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            ledgerId,
            amount: 500m,
            percentage: 2.5m);

        sundry.LedgerId.Should().Be(ledgerId);
        sundry.Amount.Amount.Should().Be(500m);
        sundry.Percentage.Should().Be(2.5m);
        sundry.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public void Constructor_ZeroOrNegativeAmount_ThrowsDomainException()
    {
        var act = () => new PurchaseInvoiceSundryLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            amount: 0m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Sundry amount must be greater than zero*");
    }

    [Fact]
    public void UpdateSequenceNumber_ValidNumber_UpdatesSuccessfully()
    {
        var sundry = new PurchaseInvoiceSundryLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            100m);

        sundry.UpdateSequenceNumber(2);
        sundry.SequenceNumber.Should().Be(2);
    }
}

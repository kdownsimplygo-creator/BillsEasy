using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class StockMovementTests
{
    [Fact]
    public void Constructor_ValidInwardMovement_SetsPropertiesAndCalculatesTotalValue()
    {
        var movement = new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2023, 4, 6),
            StockMovementType.PurchaseInward,
            "PurchaseInvoice",
            Guid.NewGuid(),
            quantity: 5m,
            Guid.NewGuid(),
            rate: 5500m,
            materialCentre: "Main Store");

        movement.Quantity.Should().Be(5m);
        movement.Rate.Amount.Should().Be(5500m);
        movement.TotalValue.Amount.Should().Be(27500m);
        movement.MaterialCentre.Should().Be("Main Store");
        movement.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ZeroQuantity_ThrowsDomainException()
    {
        var act = () => new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2023, 4, 6),
            StockMovementType.PurchaseInward,
            "PurchaseInvoice",
            Guid.NewGuid(),
            quantity: 0m,
            Guid.NewGuid(),
            rate: 500m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Quantity cannot be zero*");
    }

    [Fact]
    public void Constructor_NegativeRate_ThrowsDomainException()
    {
        var act = () => new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2023, 4, 6),
            StockMovementType.PurchaseInward,
            "PurchaseInvoice",
            Guid.NewGuid(),
            quantity: 1m,
            Guid.NewGuid(),
            rate: -100m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Rate cannot be negative*");
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalseAndRecordsAudit()
    {
        var movement = new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2023, 4, 6),
            StockMovementType.PurchaseInward,
            "PurchaseInvoice",
            Guid.NewGuid(),
            quantity: 1m,
            Guid.NewGuid(),
            rate: 100m);

        var now = DateTime.UtcNow;
        movement.Deactivate("test-user", now);

        movement.IsActive.Should().BeFalse();
        movement.ModifiedBy.Should().Be("test-user");
        movement.ModifiedAt.Should().Be(now);
    }
}

using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmountAndDefaultCurrency_SetsPropertiesCorrectly()
    {
        var money = new Money(1500.50m);

        money.Amount.Should().Be(1500.50m);
        money.Currency.Should().Be("INR");
        money.IsZero.Should().BeFalse();
        money.IsPositive.Should().BeTrue();
        money.IsNegative.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithNullOrWhitespaceCurrency_ThrowsDomainException(string? invalidCurrency)
    {
        Action act = () => new Money(100m, invalidCurrency!);

        act.Should().Throw<DomainException>()
            .WithMessage("*Currency code cannot be null or empty*");
    }

    [Fact]
    public void Constructor_RoundsToTwoDecimalPlacesAwayFromZero()
    {
        var money = new Money(100.555m);
        money.Amount.Should().Be(100.56m);

        var money2 = new Money(100.554m);
        money2.Amount.Should().Be(100.55m);
    }

    [Fact]
    public void Zero_ReturnsZeroAmount()
    {
        var zero = Money.Zero();

        zero.Amount.Should().Be(0m);
        zero.IsZero.Should().BeTrue();
        zero.IsPositive.Should().BeFalse();
        zero.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void Equality_WithSameAmountAndCurrency_ReturnsTrue()
    {
        var m1 = new Money(500.25m, "INR");
        var m2 = new Money(500.25m, "INR");

        (m1 == m2).Should().BeTrue();
        (m1 != m2).Should().BeFalse();
        m1.Equals(m2).Should().BeTrue();
        m1.GetHashCode().Should().Be(m2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentAmountsOrCurrencies_ReturnsFalse()
    {
        var m1 = new Money(500.25m, "INR");
        var m2 = new Money(600.00m, "INR");
        var m3 = new Money(500.25m, "USD");

        (m1 == m2).Should().BeFalse();
        (m1 == m3).Should().BeFalse();
        m1.Equals(m2).Should().BeFalse();
        m1.Equals(m3).Should().BeFalse();
    }

    [Fact]
    public void Addition_WithSameCurrency_ReturnsCorrectSum()
    {
        var m1 = new Money(1200.50m);
        var m2 = new Money(800.25m);

        var result = m1 + m2;

        result.Amount.Should().Be(2000.75m);
        result.Currency.Should().Be("INR");
    }

    [Fact]
    public void Addition_WithDifferentCurrency_ThrowsDomainException()
    {
        var m1 = new Money(100m, "INR");
        var m2 = new Money(100m, "USD");

        Action act = () => { var _ = m1 + m2; };

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot perform currency operation between 'INR' and 'USD'*");
    }

    [Fact]
    public void Subtraction_WithSameCurrency_ReturnsCorrectDifference()
    {
        var m1 = new Money(1500m);
        var m2 = new Money(400m);

        var result = m1 - m2;

        result.Amount.Should().Be(1100m);
    }

    [Fact]
    public void UnaryNegation_ReturnsNegativeAmount()
    {
        var money = new Money(250m);
        var negated = -money;

        negated.Amount.Should().Be(-250m);
        negated.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Multiplication_WithDecimal_MultipliesCorrectly()
    {
        var money = new Money(150m);
        var result = money * 3m;

        result.Amount.Should().Be(450m);

        var result2 = 2.5m * money;
        result2.Amount.Should().Be(375m);
    }

    [Fact]
    public void Division_WithValidDivisor_DividesCorrectly()
    {
        var money = new Money(1000m);
        var result = money / 4m;

        result.Amount.Should().Be(250m);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        var money = new Money(1000m);

        Action act = () => { var _ = money / 0m; };

        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void Comparison_Operators_WorkCorrectly()
    {
        var low = new Money(100m);
        var high = new Money(500m);
        var same = new Money(100m);

        (low < high).Should().BeTrue();
        (low <= high).Should().BeTrue();
        (low <= same).Should().BeTrue();
        (high > low).Should().BeTrue();
        (high >= low).Should().BeTrue();
        (low >= same).Should().BeTrue();
    }

    [Fact]
    public void ToString_WithDefaultCurrency_FormatsWithIndianRupeeSymbol()
    {
        var money = new Money(150000.50m);
        var formatted = money.ToString();

        formatted.Should().Be("₹ 150,000.50");
    }

    [Fact]
    public void ToMinorUnits_ReturnsExactPaiseInteger()
    {
        var money = new Money(1234.56m);
        money.ToMinorUnits().Should().Be(123456L);

        var zero = Money.Zero();
        zero.ToMinorUnits().Should().Be(0L);

        var small = new Money(0.05m);
        small.ToMinorUnits().Should().Be(5L);
    }

    [Fact]
    public void FromMinorUnits_ReconstructsExactMoneyAmount()
    {
        var money = Money.FromMinorUnits(123456L);
        money.Amount.Should().Be(1234.56m);
        money.Currency.Should().Be("INR");

        var zero = Money.FromMinorUnits(0L);
        zero.Amount.Should().Be(0.00m);
    }

    [Fact]
    public void SummationOfFractions_HasZeroFloatDrift()
    {
        // Adding 0.10 three times in IEEE float produces 0.30000000000000004
        // Money value object guarantees exact 0.30m with zero drift
        var m1 = new Money(0.10m);
        var m2 = new Money(0.10m);
        var m3 = new Money(0.10m);

        var total = m1 + m2 + m3;
        total.Amount.Should().Be(0.30m);
        total.ToMinorUnits().Should().Be(30L);
    }
}

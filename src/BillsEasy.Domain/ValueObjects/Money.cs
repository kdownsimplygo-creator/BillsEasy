using System;
using System.Globalization;
using BillsEasy.Domain.Common;

namespace BillsEasy.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount in a specific currency.
/// Uses decimal internally to guarantee exact arithmetic with zero floating-point drift.
/// </summary>
public readonly struct Money : IEquatable<Money>, IComparable<Money>, IComparable
{
    public const string DefaultCurrency = "INR";
    public const string DefaultCurrencySymbol = "₹";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency code cannot be null or empty.");

        // Normalize decimal to standard 2 decimal places with banker's / mid-point rounding
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money FromDecimal(decimal amount, string currency = DefaultCurrency) => new(amount, currency);

    public static implicit operator Money(decimal amount) => new(amount);

    /// <summary>
    /// Creates a Money instance from minor units (e.g. Paise for INR, Cents for USD).
    /// </summary>
    public static Money FromMinorUnits(long minorUnits, string currency = DefaultCurrency)
    {
        return new Money(minorUnits / 100m, currency);
    }

    /// <summary>
    /// Converts the monetary amount to minor units (e.g. Paise: ₹100.50 -> 10050).
    /// </summary>
    public long ToMinorUnits()
    {
        return (long)Math.Round(Amount * 100m, MidpointRounding.AwayFromZero);
    }

    public bool IsZero => Amount == 0m;
    public bool IsPositive => Amount > 0m;
    public bool IsNegative => Amount < 0m;

    public Money Abs() => new(Math.Abs(Amount), Currency);

    public static Money operator +(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value)
    {
        return new Money(-value.Amount, value.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator *(decimal multiplier, Money money)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0m)
            throw new DivideByZeroException("Cannot divide Money by zero.");

        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    public static bool operator <(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public static bool operator >(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public bool Equals(Money other)
    {
        return Amount == other.Amount &&
               string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is Money other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Amount, Currency?.ToUpperInvariant());
    }

    public int CompareTo(Money other)
    {
        AssertSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
            return 1;

        if (obj is not Money other)
            throw new ArgumentException($"Object must be of type {nameof(Money)}.", nameof(obj));

        return CompareTo(other);
    }

    public override string ToString()
    {
        var symbol = Currency == DefaultCurrency ? DefaultCurrencySymbol : Currency;
        return $"{symbol} {Amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    public string ToString(string format, IFormatProvider? formatProvider = null)
    {
        return Amount.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
    }

    private static void AssertSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Cannot perform currency operation between '{left.Currency}' and '{right.Currency}'.");
        }
    }
}

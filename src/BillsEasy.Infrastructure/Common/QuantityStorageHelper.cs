using System;
using System.Globalization;

namespace BillsEasy.Infrastructure.Common;

/// <summary>
/// Centralized helper for deterministic SQLite quantity representation.
/// Encodes C# decimal quantities to invariant 4-decimal-place TEXT ("0.0000") and decodes back.
/// Eliminates IEEE 754 floating-point drift.
/// </summary>
public static class QuantityStorageHelper
{
    private const string Format = "0.0000";

    public static string ToStorageString(decimal quantity)
    {
        return quantity.ToString(Format, CultureInfo.InvariantCulture);
    }

    public static decimal FromStorageString(string? storageValue)
    {
        if (string.IsNullOrWhiteSpace(storageValue))
            return 0m;

        if (decimal.TryParse(storageValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0m;
    }
}

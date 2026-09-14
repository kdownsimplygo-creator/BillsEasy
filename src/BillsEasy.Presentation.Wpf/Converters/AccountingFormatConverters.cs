using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BillsEasy.Presentation.Wpf.Converters;

public class IndianCurrencyConverter : IValueConverter
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "₹ 0.00";

        if (decimal.TryParse(value.ToString(), out var amount))
        {
            return $"₹ {amount.ToString("N2", IndianCulture)}";
        }

        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            var cleaned = str.Replace("₹", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleaned, out var amount))
            {
                return amount;
            }
        }
        return 0m;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class SystemBadgeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSystem && isSystem)
        {
            return "System";
        }
        return "Custom";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class SystemBadgeBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SystemBg = new(Color.FromRgb(0xDB, 0xEA, 0xFE)); // Blue-100
    private static readonly SolidColorBrush CustomBg = new(Color.FromRgb(0xF1, 0xF5, 0xF9)); // Slate-100

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSystem && isSystem)
        {
            return SystemBg;
        }
        return CustomBg;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

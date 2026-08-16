using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SystemMonitor.Presentation.Converters;

public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();
    public static readonly BoolToOpacityConverter Inverted = new() { Invert = true };

    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        if (Invert) b = !b;
        return b ? 1.0 : 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
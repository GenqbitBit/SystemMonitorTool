using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SystemMonitor.Presentation.Converters;

public class ScaleFontSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double scale = value is double d ? d : 1.0;
        double baseSize = parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var b)
            ? b
            : 14;

        double result = baseSize * scale;
        return Math.Clamp(result, baseSize * 0.7, baseSize * 1.6);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
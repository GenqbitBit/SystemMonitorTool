using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SystemMonitor.Presentation.ViewModels;

namespace SystemMonitor.Presentation.Converters;

public sealed class TemperatureTableBrushConverter : IValueConverter
{
    private static readonly Color[] Stops =
    {
        Color.FromRgb(33, 150, 243),
        Color.FromRgb(76, 175, 80),
        Color.FromRgb(255, 235, 59),
        Color.FromRgb(244, 67, 54)
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TemperatureTableRow { RawCelsius: double celsius })
            return Avalonia.Application.Current?.Resources["MetricValueBrush"] as IBrush ?? Brushes.White;

        var heat = Math.Clamp((celsius - 35.0) / (90.0 - 35.0), 0.0, 1.0);
        var scaled = heat * (Stops.Length - 1);
        var index = (int)Math.Floor(scaled);
        if (index >= Stops.Length - 1)
            return new SolidColorBrush(Stops[^1]);

        var fraction = scaled - index;
        var first = Stops[index];
        var second = Stops[index + 1];
        return new SolidColorBrush(Color.FromRgb(
            Interpolate(first.R, second.R, fraction),
            Interpolate(first.G, second.G, fraction),
            Interpolate(first.B, second.B, fraction)));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static byte Interpolate(byte first, byte second, double fraction) =>
        (byte)(first + (second - first) * fraction);
}

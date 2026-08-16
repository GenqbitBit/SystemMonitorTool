using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Converters;

/// <summary>
/// Auto-scales KB/s readings to MB/s once they cross 1024, so large network
/// figures (esp. download/upload peaks) don't render as unwieldy 4-5 digit
/// KB numbers. Only triggers for Unit == "KB/s" — Disk read/write are
/// already MB/s and pass through untouched, so this naturally scopes itself
/// to Network without needing a Category/Id check. Non-DataRate readings
/// (and anything with a TextValue) fall back to the reading's own
/// DisplayValue unchanged.
/// </summary>
public sealed class NetworkRateConverter : IValueConverter
{
    private const double KbPerMb = 1024.0;
    private const string KbUnit = "KB/s";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MetricReading reading)
            return value;

        if (reading.TextValue is not null || reading.Unit != KbUnit)
            return reading.DisplayValue;

        if (reading.Value >= KbPerMb)
        {
            var mb = reading.Value / KbPerMb;
            return $"{mb.ToString("N2", CultureInfo.InvariantCulture)} MB/s";
        }

        return $"{reading.Value.ToString("N2", CultureInfo.InvariantCulture)} {reading.Unit}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
namespace SystemMonitor.Domain.Models;
using System.Globalization;

public enum MetricKind
{
    Percentage,
    DataRate,
    DataSize,
    Temperature,
    Text
}

public class MetricReading
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public MetricKind Kind { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public double Value { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Average { get; set; }
    public string? TextValue { get; set; }
    public bool IsPrimary { get; set; } = true;

    public int? GpuIndex { get; set; }
    public bool? GpuIsIntegrated { get; set; }

    // Stable identity join key — mirrors GpuInfo.DeviceId. GpuIndex is kept
    // alongside for display ordering; this is what to key matching logic on.
    public string? GpuDeviceId { get; set; }

    public string DisplayValue => !IsAvailable
        ? "N/A"
        : TextValue is not null
        ? NormalizeText(TextValue)
        : FormatValue(Value, Kind, ResolveUnit(Kind, Unit));

    public static string ResolveUnit(MetricKind kind, string? unit) =>
        !string.IsNullOrWhiteSpace(unit)
            ? unit
            : kind switch
            {
                MetricKind.Percentage => "%",
                MetricKind.Temperature => "°C",
                MetricKind.DataSize => "GB",
                _ => string.Empty
            };

    private static string FormatValue(double value, MetricKind kind, string unit) => kind switch
    {
        MetricKind.Percentage => $"{value.ToString("0.##", CultureInfo.InvariantCulture)}{unit}",
        MetricKind.DataRate => $"{value.ToString("N2", CultureInfo.InvariantCulture)} {unit}".TrimEnd(),
        MetricKind.DataSize => FormatDataSize(value, unit).DisplayValue,
        _ => $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {unit}".TrimEnd()
    };

    public static (string Value, string Unit, string DisplayValue) FormatDataSize(double value, string unit)
    {
        if (unit == "GB" && Math.Abs(value) < 1)
        {
            var megabytes = value * 1000;
            return (megabytes.ToString("0.##", CultureInfo.InvariantCulture), "MB",
                $"{megabytes.ToString("0.##", CultureInfo.InvariantCulture)} MB");
        }

        if (unit == "MB" && Math.Abs(value) >= 1000)
        {
            var gigabytes = value / 1000;
            return (gigabytes.ToString("0.0#", CultureInfo.InvariantCulture), "GB",
                $"{gigabytes.ToString("0.0#", CultureInfo.InvariantCulture)} GB");
        }

        return (value.ToString("0.##", CultureInfo.InvariantCulture), unit,
            $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {unit}".TrimEnd());
    }

    public static string NormalizeText(string text) =>
        string.Equals(text.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase) ? "N/A" : text;
}
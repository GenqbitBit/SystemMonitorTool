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

    public string DisplayValue => TextValue is not null
    ? TextValue
    : Kind switch
    {
        MetricKind.Percentage => $"{Value}{Unit}",
        MetricKind.DataRate => $"{Value.ToString("N2", CultureInfo.InvariantCulture)} {Unit}",
        _ => $"{Value} {Unit}"
    };
}
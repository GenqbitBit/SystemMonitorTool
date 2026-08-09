namespace SystemMonitor.Domain.Models;

public enum MetricKind
{
    Percentage,
    DataRate,      // KB/s, MB/s — exact unit now comes from Unit, not inferred from Kind
    DataSize,      // GB, MB
    Temperature,
    Text           // identity facts (model, chipset) — the value lives in TextValue
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

    // New — only set for GPU-device readings (usage, VRAM, temperature);
    // mirrors TemperatureReading's GpuIndex/GpuIsIntegrated so any GPU row
    // can be matched to its physical device without parsing Label text.
    public int? GpuIndex { get; set; }
    public bool? GpuIsIntegrated { get; set; }

    public string DisplayValue => TextValue is not null
        ? TextValue
        : Unit == "%" ? $"{Value}{Unit}" : $"{Value} {Unit}";
}
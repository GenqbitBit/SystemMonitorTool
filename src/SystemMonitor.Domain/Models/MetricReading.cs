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
    public string Unit { get; set; } = string.Empty;   // e.g. "%", "GB", "MB/s", "KB/s", "°C"
    public bool IsAvailable { get; set; }
    public double Value { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Average { get; set; }

    /// <summary>For Text-kind metrics the value IS this string; numeric metrics leave it null.</summary>
    public string? TextValue { get; set; }

    /// <summary>
    /// True for a device's main/core reading (e.g. GPU Core temp); false for
    /// sub-readings under the same device (Hot Spot, Memory Junction, etc.).
    /// Defaults true — only Temperature readings for GPU devices currently
    /// set this to false, everything else is unaffected.
    /// </summary>
    public bool IsPrimary { get; set; } = true;

    /// <summary>Value formatted with its actual unit — e.g. "42%", "10.2 GB", "1200 KB/s".</summary>
    public string DisplayValue => TextValue is not null
        ? TextValue
        : Unit == "%" ? $"{Value}{Unit}" : $"{Value} {Unit}";
}
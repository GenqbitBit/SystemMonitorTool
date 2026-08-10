using System.Globalization;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

/// <summary>One row of the dashboard data table.</summary>
public sealed record MetricTableRow(
    string Category,    // e.g. "CPU", "Memory"
    string Metric,      // e.g. "Usage", "Clock"
    string Value,       // display text: "3.31" or "NTFS"
    string Unit,        // e.g. "%", "GHz"; empty for text rows
    double? RawValue)   // numeric when numeric, null for text — kept for future sort/filter
{
    /// <summary>Projects a domain reading into a display row.</summary>
    public static MetricTableRow From(MetricReading reading) => new(
        reading.Category,
        reading.Label,
        reading.TextValue ?? reading.Value.ToString("0.##", CultureInfo.InvariantCulture),
        reading.TextValue is null ? reading.Unit : string.Empty,
        reading.TextValue is null ? reading.Value : null);
}
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
    public static MetricTableRow From(MetricReading reading)
    {
        if (reading.TextValue is not null)
        {
            return new(reading.Category, reading.Label,
                MetricReading.NormalizeText(reading.TextValue), string.Empty, null);
        }

        if (!reading.IsAvailable)
        {
            return new(reading.Category, reading.Label, "N/A", string.Empty, null);
        }

        var unit = MetricReading.ResolveUnit(reading.Kind, reading.Unit);
        if (reading.Kind == MetricKind.DataSize)
        {
            var formatted = MetricReading.FormatDataSize(reading.Value, unit);
            return new(reading.Category, reading.Label, formatted.Value, formatted.Unit, reading.Value);
        }

        return new(reading.Category, reading.Label,
            reading.Value.ToString("0.##", CultureInfo.InvariantCulture), unit, reading.Value);
    }
}
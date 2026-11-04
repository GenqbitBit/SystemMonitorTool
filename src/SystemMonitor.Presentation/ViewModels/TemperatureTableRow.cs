using System.Globalization;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

public sealed record TemperatureTableRow(
    string Category,
    string Metric,
    string Celsius,
    string Fahrenheit,
    double? RawCelsius)
{
    public string Id { get; init; } = string.Empty;

    public static TemperatureTableRow From(MetricReading reading)
    {
        if (!reading.IsAvailable)
            return new(reading.Category, reading.Label, "Unavailable", "Unavailable", null)
            {
                Id = reading.Id
            };

        var celsius = reading.Value;
        var fahrenheit = celsius * 9.0 / 5.0 + 32.0;
        return new(
            reading.Category,
            reading.Label,
            $"{celsius.ToString("0.##", CultureInfo.InvariantCulture)} °C",
            $"{fahrenheit.ToString("0.##", CultureInfo.InvariantCulture)} °F",
            celsius)
        {
            Id = reading.Id
        };
    }
}

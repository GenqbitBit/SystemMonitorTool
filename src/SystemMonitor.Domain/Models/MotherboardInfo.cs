using System.Collections.Generic;

namespace SystemMonitor.Domain.Models;


public sealed record MotherboardInfo(
    string Model,
    string Chipset,
    double? TemperatureCelsius)
{
    // Keep every board/Super I/O sensor so the temperature table can show
    // unavailable telemetry instead of silently omitting detected sensors.
    public List<TemperatureReading> Temperatures { get; init; } = new();
}
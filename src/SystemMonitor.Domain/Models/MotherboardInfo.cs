namespace SystemMonitor.Domain.Models;

/// <summary>
/// What we know about the motherboard right now.
/// Temperature may be null on boards that expose no sensor.
/// </summary>
public sealed record MotherboardInfo(
    string Model,
    string Chipset,
    double? TemperatureCelsius);
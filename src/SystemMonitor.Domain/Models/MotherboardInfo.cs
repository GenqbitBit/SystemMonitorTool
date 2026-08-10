namespace SystemMonitor.Domain.Models;


public sealed record MotherboardInfo(
    string Model,
    string Chipset,
    double? TemperatureCelsius);
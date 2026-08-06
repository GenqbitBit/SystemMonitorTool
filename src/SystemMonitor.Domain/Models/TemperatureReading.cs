namespace SystemMonitor.Domain.Models;

public class TemperatureReading
{
    public string ComponentLabel { get; set; } = string.Empty; // "CPU", "GPU", "Disk (C:)"
    public bool IsAvailable { get; set; }
    public double TemperatureCelsius { get; set; }
}
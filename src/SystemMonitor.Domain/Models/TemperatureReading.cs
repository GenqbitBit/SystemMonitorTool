namespace SystemMonitor.Domain.Models;

public class TemperatureReading
{
    public string Category { get; set; } = string.Empty;
    public string SensorLabel { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public double TemperatureCelsius { get; set; }
    public double MinCelsius { get; set; }
    public double MaxCelsius { get; set; }
    public double AverageCelsius { get; set; }
}
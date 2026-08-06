namespace SystemMonitor.Domain.Models;

public class TemperatureReading
{
    public string Category { get; set; } = string.Empty;    
    public string SensorLabel { get; set; } = string.Empty; 
    public bool IsAvailable { get; set; }
    public double TemperatureCelsius { get; set; }
}
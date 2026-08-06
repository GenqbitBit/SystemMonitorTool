using System.Collections.Generic;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeTemperatureMonitorService : ITemperatureMonitorService
{
    public List<TemperatureReading> GetCurrentUsage() => new()
    {
        new TemperatureReading { Category = "CPU", SensorLabel = "Core (Tctl/Tdie)", IsAvailable = false },
        new TemperatureReading { Category = "GPU", SensorLabel = "GPU Core", IsAvailable = true, TemperatureCelsius = 45.0, MinCelsius = 38.0, MaxCelsius = 58.0, AverageCelsius = 46.3 },
        new TemperatureReading { Category = "GPU", SensorLabel = "GPU Hot Spot", IsAvailable = true, TemperatureCelsius = 52.2, MinCelsius = 44.0, MaxCelsius = 63.5, AverageCelsius = 53.1 },
        new TemperatureReading { Category = "Disk", SensorLabel = "Temperature #1", IsAvailable = true, TemperatureCelsius = 40.0, MinCelsius = 36.0, MaxCelsius = 42.0, AverageCelsius = 39.5 }
    };
}
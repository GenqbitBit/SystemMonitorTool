using System.Collections.Generic;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeTemperatureMonitorService : ITemperatureMonitorService
{
    public List<TemperatureReading> GetCurrentUsage() => new()
    {
        new TemperatureReading { Category = "CPU", SensorLabel = "Core (Tctl/Tdie)", IsAvailable = false, TemperatureCelsius = 0 },
        new TemperatureReading { Category = "GPU", SensorLabel = "GPU Core", IsAvailable = true, TemperatureCelsius = 45.0 },
        new TemperatureReading { Category = "GPU", SensorLabel = "GPU Hot Spot", IsAvailable = true, TemperatureCelsius = 52.2 },
        new TemperatureReading { Category = "Disk", SensorLabel = "Temperature #1", IsAvailable = true, TemperatureCelsius = 40.0 },
        new TemperatureReading { Category = "Disk", SensorLabel = "Temperature #2", IsAvailable = true, TemperatureCelsius = 40.0 }
    };
}
using System.Collections.Generic;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeTemperatureMonitorService : ITemperatureMonitorService
{
    public List<TemperatureReading> GetCurrentUsage() => new()
    {
        new TemperatureReading { ComponentLabel = "CPU - Core (Tctl/Tdie)", IsAvailable = true, TemperatureCelsius = 0.0 },
        new TemperatureReading { ComponentLabel = "GPU - GPU VR SoC", IsAvailable = true, TemperatureCelsius = 48.0 },
        new TemperatureReading { ComponentLabel = "GPU - GPU Core", IsAvailable = true, TemperatureCelsius = 48.0 },
        new TemperatureReading { ComponentLabel = "GPU - GPU Hot Spot", IsAvailable = true, TemperatureCelsius = 56.5 },
        new TemperatureReading { ComponentLabel = "GPU - GPU Memory Junction", IsAvailable = true, TemperatureCelsius = 46.0 },
        new TemperatureReading { ComponentLabel = "Disk - Temperature #1", IsAvailable = true, TemperatureCelsius = 38.0 },
        new TemperatureReading { ComponentLabel = "Disk - Temperature #2", IsAvailable = true, TemperatureCelsius = 41.5 }
    };
}
using System.Collections.Generic;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IHardwareRefreshService
{
    void RefreshAll();

    IReadOnlyList<TemperatureReading> GetTemperatureReadings() =>
        System.Array.Empty<TemperatureReading>();
}

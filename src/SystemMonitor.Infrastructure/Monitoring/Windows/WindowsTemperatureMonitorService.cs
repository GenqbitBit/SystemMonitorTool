using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsTemperatureMonitorService : ITemperatureMonitorService, IDisposable
{
    private readonly Computer _computer;
    private readonly Dictionary<ISensor, (double Sum, int Count)> _averageTracking = new();

    public WindowsTemperatureMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true
        };

        _computer.Open();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
                subHardware.Update();
        }
    }

    public List<TemperatureReading> GetCurrentUsage()
    {
        var readings = new List<TemperatureReading>();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            CollectTemperatureSensors(hardware, readings);

            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
                CollectTemperatureSensors(subHardware, readings);
            }
        }

        DisambiguateDuplicateLabels(readings);

        return readings;
    }

    private void CollectTemperatureSensors(IHardware hardware, List<TemperatureReading> readings)
    {
        var category = hardware.HardwareType switch
        {
            HardwareType.Cpu => "CPU",
            HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
            HardwareType.Storage => "Disk",
            HardwareType.Motherboard => "Motherboard",
            _ => null
        };

        if (category is null) return;

        var temperatureSensors = hardware.Sensors
            .Where(s => s.SensorType == SensorType.Temperature)
            .Where(s => !s.Name.Contains("Warning", StringComparison.OrdinalIgnoreCase)
                     && !s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                     && !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                     && !s.Name.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase));

        foreach (var sensor in temperatureSensors)
        {
            // A reading of exactly 0°C is never real for these components — see
            // investigation notes: confirmed not permissions/version/AV related,
            // appears to be OEM firmware restricting SMU telemetry on some laptops.
            var isRealReading = sensor.Value.HasValue && sensor.Value.Value != 0;

            double average = 0;
            double min = 0;
            double max = 0;

            if (isRealReading)
            {
                var currentValue = sensor.Value!.Value;

                if (_averageTracking.TryGetValue(sensor, out var existing))
                {
                    var newSum = existing.Sum + currentValue;
                    var newCount = existing.Count + 1;
                    _averageTracking[sensor] = (newSum, newCount);
                    average = newSum / newCount;
                }
                else
                {
                    _averageTracking[sensor] = (currentValue, 1);
                    average = currentValue;
                }

                // Min/Max are tracked natively by the library since Computer.Open() —
                // no need to compute these ourselves
                min = sensor.Min ?? currentValue;
                max = sensor.Max ?? currentValue;
            }

            readings.Add(new TemperatureReading
            {
                Category = category,
                SensorLabel = sensor.Name,
                IsAvailable = isRealReading,
                TemperatureCelsius = isRealReading ? sensor.Value!.Value : 0,
                MinCelsius = min,
                MaxCelsius = max,
                AverageCelsius = average
            });
        }
    }

    private static void DisambiguateDuplicateLabels(List<TemperatureReading> readings)
    {
        var groups = readings.GroupBy(r => (r.Category, r.SensorLabel));

        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var index = 1;
            foreach (var reading in group)
            {
                reading.SensorLabel = $"{reading.SensorLabel} #{index}";
                index++;
            }
        }
    }

    public void Dispose()
    {
        _computer.Close();
    }
}
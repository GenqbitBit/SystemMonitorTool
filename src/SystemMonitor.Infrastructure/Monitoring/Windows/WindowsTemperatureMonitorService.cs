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

        // Warm-up pass — same reasoning as CpuMonitorService's PerformanceCounter
        // warm-up: some sensors return meaningless values on their first
        // Update() call and only report real data from the second call onward.
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
            }
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

        return readings;
    }

    private static void CollectTemperatureSensors(IHardware hardware, List<TemperatureReading> readings)
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
                     && !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase));

        foreach (var sensor in temperatureSensors)
        {
            // Some systems report 0°C when the temperature sensor is present but unreadable.
            var isRealReading = sensor.Value.HasValue && sensor.Value.Value != 0;

            readings.Add(new TemperatureReading
            {
                ComponentLabel = $"{category} - {sensor.Name}",
                IsAvailable = isRealReading,
                TemperatureCelsius = isRealReading ? sensor.Value!.Value : 0
            });
        }
    }

    public void Dispose()
    {
        _computer.Close();
    }
}
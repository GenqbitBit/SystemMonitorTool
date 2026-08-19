using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsCpuMonitorService : ICpuMonitorService
{
    private readonly PerformanceCounter _cpuCounter;

    // OS-provided current frequency in MHz. Unlike the LibreHardwareMonitor
    // clock sensor, this needs no kernel driver — it always works.
    private readonly PerformanceCounter? _frequencyCounter;

    // Static facts, read once — they never change while running.
    private readonly string _modelName = ReadCpuModelName();
    private readonly int _coreCount;
    private readonly int _threadCount;

    // Shared Computer/driver handle — same instance every monitor service borrows.
    private readonly Computer _computer = LibreHardwareMonitorHost.Instance.Computer;

    // Per-sensor running average, keyed by the ISensor reference itself so it
    // survives across GetCurrentUsage() calls for the lifetime of this service.
    private readonly Dictionary<ISensor, (double Sum, int Count)> _temperatureAveraging = new();

    public WindowsCpuMonitorService()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue(); // first call always returns 0 — "warms up" the counter

        try
        {
            _frequencyCounter = new PerformanceCounter(
                "Processor Information", "Processor Frequency", "_Total");
            _frequencyCounter.NextValue(); // warm-up, same reason
        }
        catch
        {
            _frequencyCounter = null; // ancient Windows without this counter
        }

        (_coreCount, _threadCount) = ReadCoreCounts();
    }

    public CpuInfo GetCurrentUsage()
    {
        return new CpuInfo
        {
            UsagePercent = _cpuCounter.NextValue(),
            ModelName = _modelName,
            ClockMhz = _frequencyCounter?.NextValue(),
            CoreCount = _coreCount,
            ThreadCount = _threadCount,
            PackagePowerWatts = LibreHardwareMonitorHost.Instance
                .GetPackagePowerWatts(HardwareType.Cpu),
            Temperatures = GetTemperatures()
        };
    }

    // Moved from the old WindowsTemperatureMonitorService — same sensor
    // filtering and 0°C-is-fake handling, scoped to just this device's hardware.
    private List<TemperatureReading> GetTemperatures()
    {
        var readings = new List<TemperatureReading>();

        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu) continue;
                hardware.Update();

                var temperatureSensors = hardware.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature)
                    .Where(s => !s.Name.Contains("Warning", StringComparison.OrdinalIgnoreCase)
                             && !s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                             && !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                             && !s.Name.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase));

                // CPU has no "primary vs sub-reading" distinction like GPU does —
                // every CPU temp sensor (Package, per-core, per-CCD) is reported
                // as primary, matching the old service's behavior for category != GPU.
                readings.AddRange(temperatureSensors.Select(sensor => BuildTemperatureReading(sensor, isPrimary: true)));
            }
        }

        DisambiguateDuplicateLabels(readings);
        return readings;
    }

    private TemperatureReading BuildTemperatureReading(ISensor sensor, bool isPrimary)
    {
        // A reading of exactly 0°C is never real for these components — see
        // investigation notes: confirmed not permissions/version/AV related,
        // appears to be OEM firmware restricting SMU telemetry on some laptops.
        var isRealReading = sensor.Value.HasValue && sensor.Value.Value != 0;
        double average = 0, min = 0, max = 0;

        if (isRealReading)
        {
            var currentValue = sensor.Value!.Value;
            if (_temperatureAveraging.TryGetValue(sensor, out var existing))
            {
                var newSum = existing.Sum + currentValue;
                var newCount = existing.Count + 1;
                _temperatureAveraging[sensor] = (newSum, newCount);
                average = newSum / newCount;
            }
            else
            {
                _temperatureAveraging[sensor] = (currentValue, 1);
                average = currentValue;
            }
            // Min/Max are tracked natively by the library since Computer.Open() —
            // no need to compute these ourselves.
            min = sensor.Min ?? currentValue;
            max = sensor.Max ?? currentValue;
        }

        return new TemperatureReading
        {
            SensorLabel = sensor.Name ?? "Unknown",
            IsAvailable = isRealReading,
            TemperatureCelsius = isRealReading ? sensor.Value!.Value : 0,
            MinCelsius = min,
            MaxCelsius = max,
            AverageCelsius = average,
            IsPrimary = isPrimary
        };
    }

    private static void DisambiguateDuplicateLabels(List<TemperatureReading> readings)
    {
        var groups = readings.GroupBy(r => r.SensorLabel);
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

    // The registry needs no privilege and no vendor SDK — identity data
    // should not depend on the flaky SMU/driver channel.
    private static string ReadCpuModelName()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string ?? string.Empty).Trim();
    }

    // WMI, driver-free. NumberOfCores = physical cores;
    // NumberOfLogicalProcessors = threads (cores × SMT).
    private static (int Cores, int Threads) ReadCoreCounts()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            int cores = 0, threads = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                cores += Convert.ToInt32(obj["NumberOfCores"]);
                threads += Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
            }
            if (cores > 0 && threads > 0) return (cores, threads);
        }
        catch
        {
            // fall through to the OS-level fallback
        }
        return (Environment.ProcessorCount, Environment.ProcessorCount);
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

[SupportedOSPlatform("windows")]
public class WindowsCpuMonitorService : ICpuMonitorService, IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;

    // OS-provided current frequency in MHz. Unlike the LibreHardwareMonitor
    // clock sensor, this needs no kernel driver — it always works.
    private readonly PerformanceCounter? _frequencyCounter;

    // Static facts, read once — they never change while running.
    private readonly string _modelName = ReadCpuModelName();
    private readonly int _coreCount;
    private readonly int _threadCount;

    // Running average per sensor, keyed by stable sensor id. The host's cached
    // readings are shared between services, so they must not be mutated.
    private readonly Dictionary<string, (double Sum, int Count)> _temperatureAveraging = new();

    public WindowsCpuMonitorService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // first call always returns 0 — "warms up" the counter
        }
        catch
        {
            _cpuCounter = null;
        }

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
            UsagePercent = _cpuCounter?.NextValue() ?? 0,
            ModelName = _modelName,
            ClockMhz = _frequencyCounter?.NextValue(),
            CoreCount = _coreCount,
            ThreadCount = _threadCount,
            PackagePowerWatts = LibreHardwareMonitorHost.Instance
                .GetPackagePowerWatts(HardwareType.Cpu),
            Temperatures = GetTemperatures()
        };
    }

    // Reads the shared per-refresh temperature cache instead of walking every
    // hardware/sensor on each tick. The filter mirrors the previous behaviour:
    // firmware limit/distance sensors are not real temperatures.
    private List<TemperatureReading> GetTemperatures()
    {
        var cached = LibreHardwareMonitorHost.Instance.GetTemperatureReadings("CPU");
        if (cached.Count == 0)
            return new List<TemperatureReading>();

        var readings = new List<TemperatureReading>(cached.Count);
        foreach (var reading in cached)
        {
            if (IsLimitSensor(reading.SensorLabel))
                continue;
            readings.Add(WithAverage(reading));
        }

        return readings;
    }

    private static bool IsLimitSensor(string label) =>
        label.Contains("Warning", StringComparison.OrdinalIgnoreCase)
        || label.Contains("Critical", StringComparison.OrdinalIgnoreCase)
        || label.Contains("Limit", StringComparison.OrdinalIgnoreCase)
        || label.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase);

    private TemperatureReading WithAverage(TemperatureReading source)
    {
        double average = 0;

        if (source.IsAvailable)
        {
            var key = source.SensorId;
            if (_temperatureAveraging.TryGetValue(key, out var existing))
            {
                var newSum = existing.Sum + source.TemperatureCelsius;
                var newCount = existing.Count + 1;
                _temperatureAveraging[key] = (newSum, newCount);
                average = newSum / newCount;
            }
            else
            {
                _temperatureAveraging[key] = (source.TemperatureCelsius, 1);
                average = source.TemperatureCelsius;
            }
        }

        return new TemperatureReading
        {
            Category = source.Category,
            HardwareName = source.HardwareName,
            SensorId = source.SensorId,
            SensorLabel = source.SensorLabel,
            IsAvailable = source.IsAvailable,
            TemperatureCelsius = source.TemperatureCelsius,
            MinCelsius = source.MinCelsius,
            MaxCelsius = source.MaxCelsius,
            AverageCelsius = average,
            IsPrimary = source.IsPrimary
        };
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

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _frequencyCounter?.Dispose();
    }
}

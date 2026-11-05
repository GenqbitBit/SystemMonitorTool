using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

/// <summary>
/// The single owner of the LibreHardwareMonitor Computer instance.
/// LibreHardwareMonitor loads a kernel driver to read hardware sensors;
/// opening more than one Computer per process risks duplicate driver
/// handles and conflicting reads. This class therefore exists exactly
/// once (static Instance) and every monitor service borrows it.
///
/// Why static instead of DI registration? It guarantees one instance
/// without touching the dependency-injection setup. If the team later
/// prefers constructor injection, convert Instance into a registered
/// singleton and inject it — the rest of the code stays the same.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LibreHardwareMonitorHost : IHardwareRefreshService, IDisposable
{
    /// <summary>The one and only instance, created on first use.</summary>
    public static LibreHardwareMonitorHost Instance { get; } = new();

    /// <summary>
    /// All hardware updates must flow through this single gate so multiple
    /// monitor services do not call Update() concurrently against the same
    /// LibreHardwareMonitor Computer instance.
    /// </summary>
    public object UpdateSyncRoot { get; } = new();

    /// <summary>The shared hardware view every service reads from.</summary>
    public Computer Computer { get; }

    private static readonly IReadOnlyList<TemperatureReading> EmptyReadings = Array.Empty<TemperatureReading>();

    // Flattened hardware + sub-hardware, built once after Open(). Walking
    // SubHardware again on every scan was pure overhead.
    private IHardware[] _hardware = Array.Empty<IHardware>();

    // Sensor handles are stable for the lifetime of the opened Computer. The
    // old lookups re-walked every hardware device and its sensor list on every
    // tick just to find one value; these indices make a per-tick read O(1).
    private readonly Dictionary<HardwareType, ISensor> _packagePowerSensors = new();
    private ISensor? _cpuClockSensor;

    private Dictionary<string, IReadOnlyList<TemperatureReading>> _temperaturesByCategory =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<TemperatureReading> _allTemperatureReadings = EmptyReadings;
    private bool _temperatureCacheValid;
    private bool _disposed;

    public void RefreshAll()
    {
        lock (UpdateSyncRoot)
        {
            if (_disposed)
                return;

            var hardware = _hardware;
            for (var i = 0; i < hardware.Length; i++)
            {
                try
                {
                    hardware[i].Update();
                }
                catch (Exception ex)
                {
                    // One failing device must not abort the whole scan.
                    Debug.WriteLine($"[Hardware] Update failed for '{hardware[i].Name}': {ex.Message}");
                }
            }

            RebuildTemperatureCacheLocked();
        }
    }

    public IReadOnlyList<TemperatureReading> GetTemperatureReadings()
    {
        lock (UpdateSyncRoot)
        {
            EnsureTemperatureCacheLocked();
            return _allTemperatureReadings;
        }
    }

    /// <summary>
    /// Cached temperature readings for one category ("CPU", "GPU",
    /// "MOTHERBOARD", "STORAGE", "RAM", ...). Filtering is a dictionary
    /// lookup here instead of a full hardware walk in every service.
    /// </summary>
    public IReadOnlyList<TemperatureReading> GetTemperatureReadings(string category)
    {
        lock (UpdateSyncRoot)
        {
            EnsureTemperatureCacheLocked();
            return _temperaturesByCategory.TryGetValue(category, out var readings)
                ? readings
                : EmptyReadings;
        }
    }

    // Resolves the handful of sensors the monitor services read every tick and
    // keeps the references, so no tick has to enumerate sensors again.
    private void IndexSensorsLocked()
    {
        _packagePowerSensors.Clear();
        _cpuClockSensor = null;

        foreach (var hardware in Computer.Hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Power
                    && sensor.Name.Contains("Package", StringComparison.Ordinal)
                    && !_packagePowerSensors.ContainsKey(hardware.HardwareType))
                {
                    _packagePowerSensors[hardware.HardwareType] = sensor;
                }

                if (_cpuClockSensor is null
                    && hardware.HardwareType == HardwareType.Cpu
                    && sensor.SensorType == SensorType.Clock
                    && sensor.Name == "Cores (Average)")
                {
                    _cpuClockSensor = sensor;
                }
            }
        }
    }

    private void EnsureTemperatureCacheLocked()
    {
        if (_temperatureCacheValid || _disposed)
            return;

        // The constructor already updated every sensor; building the cache
        // from current values costs no extra Update().
        RebuildTemperatureCacheLocked();
    }

    private void RebuildTemperatureCacheLocked()
    {
        var all = new List<TemperatureReading>(64);
        Dictionary<string, List<TemperatureReading>>? byCategory = null;

        foreach (var hw in _hardware)
        {
            var category = CategoryOf(hw.HardwareType);
            byCategory ??= new Dictionary<string, List<TemperatureReading>>(StringComparer.OrdinalIgnoreCase);
            if (!byCategory.TryGetValue(category, out var categoryList))
            {
                categoryList = new List<TemperatureReading>();
                byCategory[category] = categoryList;
            }

            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature)
                    continue;

                var value = sensor.Value;
                var available = value is { } current
                    && float.IsFinite(current) && current > 0 && current <= 125;
                var sensorName = sensor.Name ?? "Unknown";
                var label = string.IsNullOrWhiteSpace(hw.Name)
                    ? sensorName
                    : $"{hw.Name}: {sensorName}";
                var currentValue = available ? value!.Value : 0;

                var reading = new TemperatureReading
                {
                    Category = category,
                    HardwareName = hw.Name,
                    SensorId = $"{hw.Identifier}/{sensor.Identifier}",
                    SensorLabel = label,
                    IsAvailable = available,
                    TemperatureCelsius = currentValue,
                    MinCelsius = available ? sensor.Min ?? currentValue : 0,
                    MaxCelsius = available ? sensor.Max ?? currentValue : 0,
                    AverageCelsius = currentValue,
                    IsPrimary = true
                };

                all.Add(reading);
                categoryList.Add(reading);
            }
        }

        var categoryIndex = new Dictionary<string, IReadOnlyList<TemperatureReading>>(
            byCategory?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
        if (byCategory is not null)
        {
            foreach (var pair in byCategory)
                categoryIndex[pair.Key] = pair.Value;
        }

        _allTemperatureReadings = all;
        _temperaturesByCategory = categoryIndex;
        _temperatureCacheValid = true;
    }

    private LibreHardwareMonitorHost()
    {
        Computer = new Computer
        {
            IsBatteryEnabled = true,
            IsCpuEnabled = true,
            IsControllerEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
        };
        Computer.Open();

        // Warm-up pass so the very first real read already has data behind it.
        lock (UpdateSyncRoot)
        {
            var flat = new List<IHardware>(16);
            var pending = new Stack<IHardware>();
            foreach (var hardware in Computer.Hardware)
                pending.Push(hardware);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                flat.Add(current);
                foreach (var sub in current.SubHardware)
                    pending.Push(sub);
            }

            _hardware = flat.ToArray();
            foreach (var hardware in _hardware)
            {
                try
                {
                    hardware.Update();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Hardware] Warm-up update failed for '{hardware.Name}': {ex.Message}");
                }
            }

            IndexSensorsLocked();
            RebuildTemperatureCacheLocked();
        }
    }

    /// <summary>
    /// Package power draw in watts ("CPU Package" / "GPU Package" sensor).
    /// Returns null when the sensor is missing OR reads exactly zero —
    /// zero watts is never physically real, it means the channel failed.
    /// </summary>
    public double? GetPackagePowerWatts(HardwareType type)
    {
        lock (UpdateSyncRoot)
        {
            if (!_packagePowerSensors.TryGetValue(type, out var sensor))
                return null;

            return PositiveOrNull(sensor.Value);
        }
    }

    /// <summary>
    /// Average core clock in MHz ("Cores (Average)" sensor).
    /// Exact name match on purpose: "Cores (Average Effective)" also
    /// CONTAINS "Cores (Average", so a substring test would be ambiguous.
    /// </summary>
    public double? GetCpuClockMhz()
    {
        lock (UpdateSyncRoot)
        {
            return _cpuClockSensor is null ? null : PositiveOrNull(_cpuClockSensor.Value);
        }
    }

    private static double? PositiveOrNull(float? value) =>
        value is { } v && v > 0 ? v : null;

    private static string CategoryOf(HardwareType type) => type switch
    {
        HardwareType.Cpu => "CPU",
        HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel => "GPU",
        HardwareType.Motherboard or HardwareType.SuperIO => "MOTHERBOARD",
        HardwareType.Storage => "STORAGE",
        HardwareType.Memory => "RAM",
        HardwareType.Battery => "BATTERY",
        _ => "OTHER"
    };

    public void Dispose()
    {
        lock (UpdateSyncRoot)
        {
            if (_disposed)
                return;

            _disposed = true;
            _temperatureCacheValid = false;
            _packagePowerSensors.Clear();
            _cpuClockSensor = null;
            _allTemperatureReadings = EmptyReadings;
            _temperaturesByCategory = new Dictionary<string, IReadOnlyList<TemperatureReading>>(
                StringComparer.OrdinalIgnoreCase);
            _hardware = Array.Empty<IHardware>();
            Computer.Close();
        }
    }
}
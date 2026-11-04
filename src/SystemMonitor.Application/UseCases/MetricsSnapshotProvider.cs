﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.UseCases;

public class MetricsSnapshotProvider : IMetricsSnapshotProvider
{
    private readonly ICpuMonitorService _cpu;
    private readonly IMemoryMonitorService _memory;
    private readonly IDiskMonitorService _disk;
    private readonly INetworkMonitorService _network;
    private readonly IMotherboardMonitorService _motherboard;
    private readonly IGpuMonitorService _gpu;
    private readonly IOsMonitorService _os;
    private readonly IMetricHistoryStore _historyStore;
    private readonly IHardwareRefreshService? _hardwareRefresh;
    private readonly Dictionary<string, RunningAverageWindow> _smoothingWindows = new();
    private readonly Dictionary<(string Category, string Suffix), string> _temperatureMetricIds = new();
    private readonly HashSet<string> _temperatureCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _staleGpuDeviceIds = new();
    private readonly Dictionary<string, (int Index, bool Integrated, string Name, string Suffix)> _gpuLabels = new();
    private readonly object _hardwareRefreshGate = new();
    private DateTime _lastHardwareRefreshUtc = DateTime.MinValue;
    private IReadOnlyList<TemperatureReading> _lastHardwareTemperatures = Array.Empty<TemperatureReading>();
    private bool _hardwareTemperaturesInitialized;
    private readonly HashSet<string> _activeMetricIds = new(StringComparer.Ordinal);
    private readonly List<string> _staleMetricIds = new();
    private static readonly TimeSpan HardwareRefreshInterval = TimeSpan.FromSeconds(2);
    private const int SmoothingWindow = 4;
    private const int DecimalPlaces = 2;

    public MetricsSnapshotProvider(
        ICpuMonitorService cpu,
        IMemoryMonitorService memory,
        IDiskMonitorService disk,
        INetworkMonitorService network,
        IMotherboardMonitorService motherboard,
        IGpuMonitorService gpu,
        IOsMonitorService os,
        IMetricHistoryStore historyStore,
        IHardwareRefreshService? hardwareRefresh = null)
    {
        _cpu = cpu;
        _memory = memory;
        _disk = disk;
        _network = network;
        _motherboard = motherboard;
        _gpu = gpu;
        _os = os;
        _historyStore = historyStore;
        _hardwareRefresh = hardwareRefresh;
    }

    public IReadOnlyList<MetricReading> GetSnapshot()
    {
        try
        {
            return GetSnapshotCore();
        }
        catch
        {
            return MetricCatalog.All
                .Where(entry => !entry.Id.Contains(".0") && !entry.Id.Contains(".1")
                    && !entry.Id.StartsWith("temp.", StringComparison.Ordinal))
                .Select(BuildUnavailableReading)
                .ToList();
        }
    }

    private IReadOnlyList<MetricReading> GetSnapshotCore()
    {
        var hardwareRefreshed = TryRefreshHardware();
        IReadOnlyList<TemperatureReading>? hardwareTemperatures = null;
        if (_hardwareRefresh is not null)
        {
            // Temperature readings are produced by the same hardware scan the
            // provider already throttles to 2 s. Re-reading them on every tick
            // (as the old code did) forced an extra full sensor walk per tick.
            if (hardwareRefreshed || !_hardwareTemperaturesInitialized)
            {
                try
                {
                    _lastHardwareTemperatures = _hardwareRefresh.GetTemperatureReadings();
                    _hardwareTemperaturesInitialized = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Temperature] hardware scan failed; using component readings: {ex}");
                }
            }
            hardwareTemperatures = _lastHardwareTemperatures;
        }

        var readings = new List<MetricReading>(96);

        // CPU — identity rows first (emission order = display order).
        var cpuInfo = ReadService(_cpu.GetCurrentUsage, new CpuInfo(), "CPU", out var cpuServiceAvailable);
        var cpuAvailable = cpuServiceAvailable && !string.IsNullOrWhiteSpace(cpuInfo.ModelName)
            && !(OperatingSystem.IsMacOS()
                && cpuInfo.ModelName.Contains("implementation pending", StringComparison.OrdinalIgnoreCase));
        readings.Add(BuildTextReading(MetricCatalog.CpuModel, cpuInfo.ModelName, cpuAvailable));
        var cpuUsageReading = BuildReading(MetricCatalog.CpuUsage, cpuInfo.UsagePercent, smooth: true, available: cpuAvailable);
        readings.Add(cpuUsageReading);
        readings.Add(BuildPeakReading(MetricCatalog.CpuUsagePeak, MetricCatalog.CpuUsage.Id, cpuAvailable));
        readings.Add(BuildComplementPercentageReading(MetricCatalog.CpuAvailable, cpuUsageReading));
        readings.Add(BuildTextReading(MetricCatalog.CpuClock, FormatClock(cpuInfo.ClockMhz), cpuAvailable));
        readings.Add(BuildTextReading(MetricCatalog.CpuCores, cpuInfo.CoreCount.ToString(), cpuAvailable));
        readings.Add(BuildTextReading(MetricCatalog.CpuThreads, cpuInfo.ThreadCount.ToString(), cpuAvailable));

        var boardInfo = ReadService(_motherboard.GetCurrentInfo, null, "Motherboard", out var boardServiceAvailable);
        if (boardInfo is not null)
        {
            readings.Add(BuildTextReading(MetricCatalog.MotherboardModel, boardInfo.Model, boardServiceAvailable));
            readings.Add(BuildTextReading(MetricCatalog.MotherboardChipset, boardInfo.Chipset, boardServiceAvailable));
        }

        // Memory — identity rows first (Name leads, mirroring CPU/Disk "Model"),
        // then live usage.
        var memInfo = ReadService(_memory.GetCurrentUsage, new MemoryInfo(), "Memory", out var memoryServiceAvailable);
        var usedGB = memInfo.UsedMB / 1024.0;
        var totalGB = memInfo.TotalMB / 1024.0;
        readings.Add(BuildTextReading(MetricCatalog.MemoryName,
            string.IsNullOrWhiteSpace(memInfo.PartNumber) ? null : memInfo.PartNumber));
        readings.Add(BuildTextReading(MetricCatalog.MemoryType, memInfo.Type));
        readings.Add(BuildTextReading(MetricCatalog.MemorySpeed,
            memInfo.SpeedMhz > 0 ? $"{memInfo.SpeedMhz} MHz" : null));
        readings.Add(BuildTextReading(MetricCatalog.MemoryModules, memInfo.ModuleConfig));
        readings.Add(BuildTextReading(MetricCatalog.MemoryManufacturer, memInfo.Manufacturer));
        var memoryAvailable = memoryServiceAvailable && memInfo.TotalMB > 0;
        readings.Add(BuildReading(MetricCatalog.MemoryUsage, memInfo.UsagePercent, smooth: true, available: memoryAvailable));
        readings.Add(BuildPeakReading(MetricCatalog.MemoryUsagePeak, MetricCatalog.MemoryUsage.Id, memoryAvailable));
        readings.Add(BuildReading(MetricCatalog.MemoryUsed, usedGB, available: memoryAvailable));
        readings.Add(BuildReading(MetricCatalog.MemoryTotal, totalGB, available: memoryAvailable));
        readings.Add(BuildReading(MetricCatalog.MemoryFree, totalGB - usedGB, available: memoryAvailable));

        // Disk — identity rows first, then live usage.
        var diskInfo = ReadService(_disk.GetCurrentUsage, new DiskInfo(), "Disk", out var diskServiceAvailable);
        var diskAvailable = diskServiceAvailable && diskInfo.TotalGB > 0;
        var diskLabelSuffix = $" ({diskInfo.DriveName})";
        readings.Add(BuildTextReading(MetricCatalog.DiskModel, diskInfo.Model));
        readings.Add(BuildTextReading(MetricCatalog.DiskType, diskInfo.DiskType));
        readings.Add(BuildTextReading(MetricCatalog.DiskBus, diskInfo.BusType));
        readings.Add(BuildTextReading(MetricCatalog.DiskFileSystem, diskInfo.FileSystem));
        readings.Add(BuildReading(MetricCatalog.DiskUsage, diskInfo.UsagePercent, smooth: true, available: diskAvailable,
            labelOverride: MetricCatalog.DiskUsage.Label + diskLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.DiskUsed, diskInfo.UsedGB, available: diskAvailable,
            labelOverride: MetricCatalog.DiskUsed.Label + diskLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.DiskTotal, diskInfo.TotalGB, available: diskAvailable,
            labelOverride: MetricCatalog.DiskTotal.Label + diskLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.DiskRead, diskInfo.ReadMBPerSec, available: diskAvailable));
        readings.Add(BuildPeakReading(MetricCatalog.DiskReadPeak, MetricCatalog.DiskRead.Id, diskAvailable));
        readings.Add(BuildReading(MetricCatalog.DiskWrite, diskInfo.WriteMBPerSec, available: diskAvailable));
        readings.Add(BuildPeakReading(MetricCatalog.DiskWritePeak, MetricCatalog.DiskWrite.Id, diskAvailable));
        readings.Add(BuildReading(MetricCatalog.DiskFree, diskInfo.TotalGB - diskInfo.UsedGB, available: diskAvailable,
        labelOverride: MetricCatalog.DiskFree.Label + diskLabelSuffix));

        // Network
        var networkInfo = ReadService(_network.GetCurrentUsage, new NetworkInfo(), "Network", out var networkAvailable);
        readings.Add(BuildReading(MetricCatalog.NetworkDownload, networkInfo.DownloadKBPerSec, available: networkAvailable));
        readings.Add(BuildPeakReading(MetricCatalog.NetworkDownloadPeak, MetricCatalog.NetworkDownload.Id, networkAvailable));
        readings.Add(BuildReading(MetricCatalog.NetworkUpload, networkInfo.UploadKBPerSec, available: networkAvailable));
        readings.Add(BuildPeakReading(MetricCatalog.NetworkUploadPeak, MetricCatalog.NetworkUpload.Id, networkAvailable));

        // Operating System — platform-neutral, driver-free.
        // Identity first, then live counts (mirrors the CPU section's order).
        var osInfo = ReadService(_os.GetCurrentInfo, new OperatingSystemInfo(), "Operating system", out var osAvailable);
        readings.Add(BuildTextReading(MetricCatalog.OsName, osInfo.OsName, osAvailable));
        readings.Add(BuildTextReading(MetricCatalog.OsVersion, osInfo.OsVersion, osAvailable));
        readings.Add(BuildTextReading(MetricCatalog.OsUptime, FormatUptime(osInfo.Uptime), osAvailable));
        readings.Add(BuildTextReading(MetricCatalog.OsProcesses, osInfo.ProcessCount.ToString(), osAvailable));
        readings.Add(BuildTextReading(MetricCatalog.OsThreads, osInfo.ThreadCount.ToString(), osAvailable));
        readings.Add(BuildTextReading(MetricCatalog.OsHandles, FormatHandles(osInfo.HandleCount), osAvailable));

        // GPU — runtime-discovered, zero or more devices, each keyed by its
        // stable DeviceId (not enumeration Index) so identity survives even if
        // WMI's device order ever shifts between runs.
        var gpuInfos = ReadService(_gpu.GetCurrentUsage, Array.Empty<GpuInfo>(), "GPU", out _);
        var activeGpuIds = new HashSet<string>();
        foreach (var gpuInfo in gpuInfos)
        {
            if (!gpuInfo.IsAvailable) continue;
            activeGpuIds.Add(gpuInfo.DeviceId);
            var gpuUsedGB = gpuInfo.DedicatedMemoryUsedMb / 1024.0;
            var gpuTotalGB = gpuInfo.DedicatedMemoryTotalMb / 1024.0;
            var gpuLabelSuffix = GetGpuLabelSuffix(gpuInfo);
            readings.Add(BuildGpuReading(MetricCatalog.GpuUsage, gpuInfo, gpuInfo.UsagePercent, smooth: true, gpuLabelSuffix));
            readings.Add(BuildGpuReading(MetricCatalog.GpuMemoryUsed, gpuInfo, gpuUsedGB, smooth: false, gpuLabelSuffix));
            readings.Add(BuildGpuReading(MetricCatalog.GpuMemoryTotal, gpuInfo, gpuTotalGB, smooth: false, gpuLabelSuffix));
            readings.Add(BuildGpuTextReading(MetricCatalog.GpuModel, gpuInfo, gpuInfo.Name, gpuLabelSuffix));
        }

        // Only scan for stale identities when the counts disagree; the common
        // case (same devices every tick) then costs nothing instead of the
        // list-and-LINQ allocation the previous form paid on every snapshot.
        if (_gpuLabels.Count != activeGpuIds.Count)
        {
            _staleGpuDeviceIds.Clear();
            foreach (var deviceId in _gpuLabels.Keys)
            {
                if (!activeGpuIds.Contains(deviceId))
                    _staleGpuDeviceIds.Add(deviceId);
            }

            for (var i = 0; i < _staleGpuDeviceIds.Count; i++)
                _gpuLabels.Remove(_staleGpuDeviceIds[i]);
        }


        PruneSmoothingWindows(readings);
        // Prefer the full hardware-tree scan, but supplement categories the
        // shared scan did not return from their existing monitor snapshots.
        _temperatureCategories.Clear();
        if (hardwareTemperatures is not null)
        {
            foreach (var reading in hardwareTemperatures)
            {
                _temperatureCategories.Add(reading.Category);
                readings.Add(BuildTemperatureMetric(
                    reading.Category,
                    reading,
                    id: GetTemperatureMetricId(
                        reading.Category,
                        string.IsNullOrWhiteSpace(reading.SensorId)
                            ? $"{reading.HardwareName}.{reading.SensorLabel}"
                            : reading.SensorId)));
            }
        }

        AddTemperatureFallback(readings, "CPU", cpuInfo.Temperatures);
        AddTemperatureFallback(
            readings,
            "MOTHERBOARD",
            boardInfo?.Temperatures ?? (IReadOnlyList<TemperatureReading>)Array.Empty<TemperatureReading>());
        AddTemperatureFallback(readings, "RAM", memInfo.Temperatures);
        AddTemperatureFallback(readings, "STORAGE", diskInfo.Temperatures);
        if (!_temperatureCategories.Contains("GPU"))
        {
            for (var i = 0; i < gpuInfos.Count; i++)
            {
                var gpuInfo = gpuInfos[i];
                if (!gpuInfo.IsAvailable) continue;

                foreach (var reading in gpuInfo.Temperatures)
                {
                    readings.Add(BuildTemperatureMetric(
                        "GPU",
                        reading,
                        id: GetTemperatureMetricId("GPU", $"{gpuInfo.DeviceId}.{reading.SensorLabel}"),
                        gpuIndex: gpuInfo.Index,
                        gpuIsIntegrated: gpuInfo.IsIntegrated,
                        gpuDeviceId: gpuInfo.DeviceId));
                }
            }
        }

        // Sensor sets are stable in practice, so the id cache is a pure win;
        // the cap only guards against pathological hardware churn.
        if (_temperatureMetricIds.Count > 256)
            _temperatureMetricIds.Clear();

        return readings;
    }

    // Appends a category's fallback sensors only when the shared hardware scan
    // did not already provide that category. The previous local-function form
    // captured the readings list and category set in a closure allocated on
    // every single tick.
    private void AddTemperatureFallback(
        List<MetricReading> readings,
        string category,
        IReadOnlyList<TemperatureReading> fallbackReadings)
    {
        if (_temperatureCategories.Contains(category))
            return;

        var added = false;
        for (var i = 0; i < fallbackReadings.Count; i++)
        {
            var reading = fallbackReadings[i];
            readings.Add(BuildTemperatureMetric(
                category,
                reading,
                id: GetTemperatureMetricId(category, reading.SensorLabel)));
            added = true;
        }

        if (added)
            _temperatureCategories.Add(category);
    }

    // Temperature ids were rebuilt (and lower-cased) for every sensor on every
    // tick. The identity only depends on category + sensor suffix, so cache it.
    private string GetTemperatureMetricId(string category, string suffix)
    {
        var key = (category, suffix);
        if (_temperatureMetricIds.TryGetValue(key, out var id))
            return id;

        id = $"temp.{category}.{suffix}".ToLowerInvariant();
        _temperatureMetricIds[key] = id;
        return id;
    }

    private static MetricReading BuildUnavailableReading(MetricCatalogEntry entry) => new()
    {
        Id = entry.Id,
        Category = entry.Category,
        Label = entry.Label,
        Kind = entry.Kind,
        Unit = entry.Unit,
        IsAvailable = false,
        TextValue = entry.Kind == MetricKind.Text ? "N/A" : null
    };

    private static T ReadService<T>(Func<T> read, T fallback, string category, out bool available)
    {
        try
        {
            var result = read();
            available = true;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{category}] monitoring failed; category marked unavailable: {ex}");
            available = false;
            return fallback;
        }
    }

    /// <summary>Returns true when a full hardware refresh actually ran.</summary>
    private bool TryRefreshHardware()
    {
        if (_hardwareRefresh is null)
            return false;

        var now = DateTime.UtcNow;
        lock (_hardwareRefreshGate)
        {
            if (now - _lastHardwareRefreshUtc < HardwareRefreshInterval)
                return false;

            _lastHardwareRefreshUtc = now;
        }

        try
        {
            _hardwareRefresh.RefreshAll();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Hardware] refresh failed; continuing with last readable services: {ex}");
            return false;
        }
    }

    private void PruneSmoothingWindows(IReadOnlyList<MetricReading> readings)
    {
        if (_smoothingWindows.Count == 0)
            return;

        // No LINQ/enumeration snapshot here: this runs on every poll tick.
        _activeMetricIds.Clear();
        for (var i = 0; i < readings.Count; i++)
            _activeMetricIds.Add(readings[i].Id);

        _staleMetricIds.Clear();
        foreach (var id in _smoothingWindows.Keys)
        {
            if (!_activeMetricIds.Contains(id))
                _staleMetricIds.Add(id);
        }

        for (var i = 0; i < _staleMetricIds.Count; i++)
            _smoothingWindows.Remove(_staleMetricIds[i]);
    }

    private string GetGpuLabelSuffix(GpuInfo gpuInfo)
    {
        if (_gpuLabels.TryGetValue(gpuInfo.DeviceId, out var cached)
            && cached.Index == gpuInfo.Index
            && cached.Integrated == gpuInfo.IsIntegrated
            && cached.Name == gpuInfo.Name)
        {
            return cached.Suffix;
        }

        var deviceTag = gpuInfo.IsIntegrated ? "Integrated" : "Dedicated";
        var suffix = $" (GPU {gpuInfo.Index} - {deviceTag}: {gpuInfo.Name})";
        _gpuLabels[gpuInfo.DeviceId] = (gpuInfo.Index, gpuInfo.IsIntegrated, gpuInfo.Name, suffix);
        return suffix;
    }

    private MetricReading BuildReading(
        MetricCatalogEntry entry, double rawValue, bool smooth = false, string? labelOverride = null,
        bool available = true)
    {
        var value = smooth ? Smooth(entry.Id, rawValue) : rawValue;
        return new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = labelOverride ?? entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = available && !double.IsNaN(value) && !double.IsInfinity(value),
            Value = Round(value)
        };
    }

    private MetricReading BuildPeakReading(MetricCatalogEntry entry, string sourceMetricId, bool available = true)
    {
        var (_, max) = _historyStore.GetCommittedRange(sourceMetricId);
        return new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = available,
            Value = Round(max)
        };
    }

    private MetricReading BuildGpuReading(
        MetricCatalogEntry entry, GpuInfo gpuInfo, double rawValue, bool smooth, string labelSuffix)
    {
        var id = $"{entry.Id}.{gpuInfo.DeviceId}";
        var value = smooth ? Smooth(id, rawValue) : rawValue;
        return new MetricReading
        {
            Id = id,
            Category = entry.Category,
            Label = entry.Label + labelSuffix,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = true,
            Value = Round(value),
            GpuIndex = gpuInfo.Index,
            GpuIsIntegrated = gpuInfo.IsIntegrated,
            GpuDeviceId = gpuInfo.DeviceId
        };
    }

    // Turns a per-device TemperatureReading into a MetricReading. Replaces the
    // old inline loop over ITemperatureMonitorService.GetCurrentUsage() — same
    // Id scheme ("temp.{category}.{idSuffix}"), same Min/Max/Average rounding.
    private static MetricReading BuildTemperatureMetric(
        string category, TemperatureReading reading, string id,
        int? gpuIndex = null, bool? gpuIsIntegrated = null,
        string? gpuDeviceId = null) => new()
    {
        Id = id,
        Category = category,
        Label = reading.SensorLabel,
        Kind = MetricKind.Temperature,
        Unit = "°C",
        IsAvailable = reading.IsAvailable,
        Value = Round(reading.TemperatureCelsius),
        Min = RoundNullable(reading.MinCelsius),
        Max = RoundNullable(reading.MaxCelsius),
        Average = RoundNullable(reading.AverageCelsius),
        IsPrimary = reading.IsPrimary,
        GpuIndex = gpuIndex,
        GpuIsIntegrated = gpuIsIntegrated,
        GpuDeviceId = gpuDeviceId
    };

    private static MetricReading BuildGpuTextReading(
        MetricCatalogEntry entry, GpuInfo gpuInfo, string? text, string labelSuffix)
    {
        var normalized = IsMissingText(text) ? "N/A" : text!;

        return new MetricReading
        {
            Id = $"{entry.Id}.{gpuInfo.DeviceId}",
            Category = entry.Category,
            Label = entry.Label + labelSuffix,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = normalized != "N/A",
            TextValue = normalized,
            GpuIndex = gpuInfo.Index,
            GpuIsIntegrated = gpuInfo.IsIntegrated,
            GpuDeviceId = gpuInfo.DeviceId
        };
    }

    private static MetricReading BuildComplementPercentageReading(MetricCatalogEntry entry, MetricReading source)
    {
        return new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = source.IsAvailable,
            Value = Round(100 - source.Value)
        };
    }

    private static MetricReading BuildTextReading(MetricCatalogEntry entry, string? text, bool available = true)
    {
        var normalized = IsMissingText(text) ? "N/A" : text!;

        return new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = available && normalized != "N/A",
            TextValue = normalized
        };
    }

    private static string FormatWatts(double? watts) =>
        watts.HasValue ? $"{watts.Value:F2} W" : "—";

    private static string FormatClock(double? mhz) =>
        mhz.HasValue
            ? mhz.Value >= 1000 ? $"{mhz.Value / 1000:0.00} GHz" : $"{mhz.Value:0} MHz"
            : "—";

    private static string FormatUptime(TimeSpan uptime) =>
        uptime.TotalDays >= 1 ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
        : uptime.TotalHours >= 1 ? $"{uptime.Hours}h {uptime.Minutes}m"
        : $"{uptime.Minutes}m {uptime.Seconds}s";

    private static string FormatHandles(long? handles) =>
        handles.HasValue ? handles.Value.ToString("N0") : "—";

    // "Unchanged or blank text" check without allocating: the previous form
    // Trim()ed (and therefore copied) every identity string on every tick.
    private static bool IsMissingText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        return text.AsSpan().Trim().Equals("Unknown".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private double Smooth(string id, double newValue)
    {
        if (!_smoothingWindows.TryGetValue(id, out var window))
        {
            window = new RunningAverageWindow(SmoothingWindow);
            _smoothingWindows[id] = window;
        }

        return window.Add(newValue);
    }

    // Fixed-size ring buffer with a running sum. Replaces a Queue<double> whose
    // Average() call boxed an enumerator and re-summed the window on every
    // smoothed metric, on every tick.
    private sealed class RunningAverageWindow
    {
        private readonly double[] _values;
        private int _count;
        private int _next;
        private double _sum;

        public RunningAverageWindow(int capacity) => _values = new double[capacity];

        public double Add(double value)
        {
            if (_count < _values.Length)
                _count++;
            else
                _sum -= _values[_next];

            _values[_next] = value;
            _sum += value;

            if (++_next == _values.Length)
                _next = 0;

            return _sum / _count;
        }
    }

    private static double Round(double value) => Math.Round(value, DecimalPlaces);

    private static double? RoundNullable(double? value) =>
        value.HasValue ? Math.Round(value.Value, DecimalPlaces) : null;
}
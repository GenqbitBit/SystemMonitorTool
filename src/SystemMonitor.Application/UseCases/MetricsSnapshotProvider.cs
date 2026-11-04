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
    private readonly Dictionary<string, Queue<double>> _smoothingWindows = new();
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

        var readings = new List<MetricReading>();

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

        foreach (var deviceId in _gpuLabels.Keys.Where(id => !activeGpuIds.Contains(id)).ToList())
            _gpuLabels.Remove(deviceId);


        PruneSmoothingWindows(readings);
        // Prefer the full hardware-tree scan, but supplement categories the
        // shared scan did not return from their existing monitor snapshots.
        var temperatureCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hardwareTemperatures is not null)
        {
            foreach (var reading in hardwareTemperatures)
            {
                temperatureCategories.Add(reading.Category);
                readings.Add(BuildTemperatureMetric(
                    reading.Category,
                    reading,
                    idSuffix: string.IsNullOrWhiteSpace(reading.SensorId)
                        ? $"{reading.HardwareName}.{reading.SensorLabel}"
                        : reading.SensorId));
            }
        }

        void AddTemperatureFallback(
            string category,
            IEnumerable<TemperatureReading> fallbackReadings,
            Func<TemperatureReading, string>? idSuffix = null)
        {
            if (temperatureCategories.Contains(category))
                return;

            var added = false;
            foreach (var reading in fallbackReadings)
            {
                readings.Add(BuildTemperatureMetric(
                    category,
                    reading,
                    idSuffix: idSuffix?.Invoke(reading) ?? reading.SensorLabel));
                added = true;
            }

            if (added)
                temperatureCategories.Add(category);
        }

        AddTemperatureFallback("CPU", cpuInfo.Temperatures);
        AddTemperatureFallback(
            "MOTHERBOARD",
            boardInfo?.Temperatures.AsEnumerable() ?? Enumerable.Empty<TemperatureReading>());
        AddTemperatureFallback("RAM", memInfo.Temperatures);
        AddTemperatureFallback("STORAGE", diskInfo.Temperatures);
        if (!temperatureCategories.Contains("GPU"))
        {
            foreach (var gpuInfo in gpuInfos.Where(gpu => gpu.IsAvailable))
            {
                foreach (var reading in gpuInfo.Temperatures)
                {
                    readings.Add(BuildTemperatureMetric(
                        "GPU",
                        reading,
                        idSuffix: $"{gpuInfo.DeviceId}.{reading.SensorLabel}",
                        gpuIndex: gpuInfo.Index,
                        gpuIsIntegrated: gpuInfo.IsIntegrated,
                        gpuDeviceId: gpuInfo.DeviceId));
                }
            }
        }

        return readings;
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
        string category, TemperatureReading reading, string idSuffix,
        int? gpuIndex = null, bool? gpuIsIntegrated = null,
        string? gpuDeviceId = null) => new()
    {
        Id = $"temp.{category}.{idSuffix}".ToLowerInvariant(),
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
        var normalized = string.IsNullOrWhiteSpace(text)
            || string.Equals(text.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase)
            ? "N/A"
            : text;

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
        var normalized = string.IsNullOrWhiteSpace(text)
            || string.Equals(text.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase)
            ? "N/A"
            : text;

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

    private double Smooth(string id, double newValue)
    {
        if (!_smoothingWindows.TryGetValue(id, out var window))
        {
            window = new Queue<double>();
            _smoothingWindows[id] = window;
        }
        window.Enqueue(newValue);
        if (window.Count > SmoothingWindow)
            window.Dequeue();
        return window.Average();
    }

    private static double Round(double value) => Math.Round(value, DecimalPlaces);

    private static double? RoundNullable(double? value) =>
        value.HasValue ? Math.Round(value.Value, DecimalPlaces) : null;
}
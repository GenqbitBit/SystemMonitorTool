﻿using System;
using System.Collections.Generic;
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
    private readonly Dictionary<string, Queue<double>> _smoothingWindows = new();
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
        IMetricHistoryStore historyStore)
    {
        _cpu = cpu;
        _memory = memory;
        _disk = disk;
        _network = network;
        _motherboard = motherboard;
        _gpu = gpu;
        _os = os;
        _historyStore = historyStore;
    }

    public IReadOnlyList<MetricReading> GetSnapshot()
    {
        var readings = new List<MetricReading>();

        // CPU — identity rows first (emission order = display order).
        var cpuInfo = _cpu.GetCurrentUsage();
        readings.Add(BuildTextReading(MetricCatalog.CpuModel, cpuInfo.ModelName));
        var cpuUsageReading = BuildReading(MetricCatalog.CpuUsage, cpuInfo.UsagePercent, smooth: true);
        readings.Add(cpuUsageReading);
        readings.Add(BuildPeakReading(MetricCatalog.CpuUsagePeak, MetricCatalog.CpuUsage.Id));
        readings.Add(BuildComplementPercentageReading(MetricCatalog.CpuAvailable, cpuUsageReading));
        readings.Add(BuildTextReading(MetricCatalog.CpuClock, FormatClock(cpuInfo.ClockMhz)));
        readings.Add(BuildTextReading(MetricCatalog.CpuCores, cpuInfo.CoreCount.ToString()));
        readings.Add(BuildTextReading(MetricCatalog.CpuThreads, cpuInfo.ThreadCount.ToString()));

        // Motherboard — identity rows only. The temperature row was removed:
        // this machine's Super I/O channel never reports a real value, and a
        // permanent "0 °C" is noise, not telemetry.
        var boardInfo = _motherboard.GetCurrentInfo();
        if (boardInfo is not null)
        {
            readings.Add(BuildTextReading(MetricCatalog.MotherboardModel, boardInfo.Model));
            readings.Add(BuildTextReading(MetricCatalog.MotherboardChipset, boardInfo.Chipset));
        }

        // Memory — identity rows first (Name leads, mirroring CPU/Disk "Model"),
        // then live usage.
        var memInfo = _memory.GetCurrentUsage();
        var usedGB = memInfo.UsedMB / 1024.0;
        var totalGB = memInfo.TotalMB / 1024.0;
        readings.Add(BuildTextReading(MetricCatalog.MemoryName,
            string.IsNullOrWhiteSpace(memInfo.PartNumber) ? null : memInfo.PartNumber));
        readings.Add(BuildTextReading(MetricCatalog.MemoryType, memInfo.Type));
        readings.Add(BuildTextReading(MetricCatalog.MemorySpeed,
            memInfo.SpeedMhz > 0 ? $"{memInfo.SpeedMhz} MHz" : null));
        readings.Add(BuildTextReading(MetricCatalog.MemoryModules, memInfo.ModuleConfig));
        readings.Add(BuildTextReading(MetricCatalog.MemoryManufacturer, memInfo.Manufacturer));
        readings.Add(BuildReading(MetricCatalog.MemoryUsage, memInfo.UsagePercent, smooth: true));
        readings.Add(BuildPeakReading(MetricCatalog.MemoryUsagePeak, MetricCatalog.MemoryUsage.Id)); 
        readings.Add(BuildReading(MetricCatalog.MemoryUsed, usedGB));
        readings.Add(BuildReading(MetricCatalog.MemoryTotal, totalGB));
        readings.Add(BuildReading(MetricCatalog.MemoryFree, totalGB - usedGB));

        // Disk — identity rows first, then live usage.
        var diskInfo = _disk.GetCurrentUsage();
        var diskLabelSuffix = $" ({diskInfo.DriveName})";
        readings.Add(BuildTextReading(MetricCatalog.DiskModel, diskInfo.Model));
        readings.Add(BuildTextReading(MetricCatalog.DiskType, diskInfo.DiskType));
        readings.Add(BuildTextReading(MetricCatalog.DiskBus, diskInfo.BusType));
        readings.Add(BuildTextReading(MetricCatalog.DiskFileSystem, diskInfo.FileSystem));
        readings.Add(BuildReading(MetricCatalog.DiskUsage, diskInfo.UsagePercent, smooth: true,
            labelOverride: MetricCatalog.DiskUsage.Label + diskLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.DiskUsed, diskInfo.UsedGB,
            labelOverride: MetricCatalog.DiskUsed.Label + diskLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.DiskTotal, diskInfo.TotalGB,
            labelOverride: MetricCatalog.DiskTotal.Label + diskLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.DiskRead, diskInfo.ReadMBPerSec));
        readings.Add(BuildPeakReading(MetricCatalog.DiskReadPeak, MetricCatalog.DiskRead.Id)); 
        readings.Add(BuildReading(MetricCatalog.DiskWrite, diskInfo.WriteMBPerSec));
        readings.Add(BuildPeakReading(MetricCatalog.DiskWritePeak, MetricCatalog.DiskWrite.Id));
        readings.Add(BuildReading(MetricCatalog.DiskFree, diskInfo.TotalGB - diskInfo.UsedGB,
        labelOverride: MetricCatalog.DiskFree.Label + diskLabelSuffix));

        // Network
        var networkInfo = _network.GetCurrentUsage();
        readings.Add(BuildReading(MetricCatalog.NetworkDownload, networkInfo.DownloadKBPerSec));
        readings.Add(BuildPeakReading(MetricCatalog.NetworkDownloadPeak, MetricCatalog.NetworkDownload.Id));
        readings.Add(BuildReading(MetricCatalog.NetworkUpload, networkInfo.UploadKBPerSec));
        readings.Add(BuildPeakReading(MetricCatalog.NetworkUploadPeak, MetricCatalog.NetworkUpload.Id));

        // Operating System — platform-neutral, driver-free.
        // Identity first, then live counts (mirrors the CPU section's order).
        var osInfo = _os.GetCurrentInfo();
        readings.Add(BuildTextReading(MetricCatalog.OsName, osInfo.OsName));
        readings.Add(BuildTextReading(MetricCatalog.OsVersion, osInfo.OsVersion));
        readings.Add(BuildTextReading(MetricCatalog.OsUptime, FormatUptime(osInfo.Uptime)));
        readings.Add(BuildTextReading(MetricCatalog.OsProcesses, osInfo.ProcessCount.ToString()));
        readings.Add(BuildTextReading(MetricCatalog.OsThreads, osInfo.ThreadCount.ToString()));
        readings.Add(BuildTextReading(MetricCatalog.OsHandles, FormatHandles(osInfo.HandleCount)));

        // GPU — runtime-discovered, zero or more devices, each keyed by its
        // stable DeviceId (not enumeration Index) so identity survives even if
        // WMI's device order ever shifts between runs.
        var gpuInfos = _gpu.GetCurrentUsage();
        foreach (var gpuInfo in gpuInfos)
        {
            if (!gpuInfo.IsAvailable) continue;
            var gpuUsedGB = gpuInfo.DedicatedMemoryUsedMb / 1024.0;
            var gpuTotalGB = gpuInfo.DedicatedMemoryTotalMb / 1024.0;
            var deviceTag = gpuInfo.IsIntegrated ? "Integrated" : "Dedicated";
            var gpuLabelSuffix = $" (GPU {gpuInfo.Index} - {deviceTag}: {gpuInfo.Name})";
            readings.Add(BuildGpuReading(MetricCatalog.GpuUsage, gpuInfo, gpuInfo.UsagePercent, smooth: true, gpuLabelSuffix));
            readings.Add(BuildGpuReading(MetricCatalog.GpuMemoryUsed, gpuInfo, gpuUsedGB, smooth: false, gpuLabelSuffix));
            readings.Add(BuildGpuReading(MetricCatalog.GpuMemoryTotal, gpuInfo, gpuTotalGB, smooth: false, gpuLabelSuffix));
            readings.Add(BuildGpuTextReading(MetricCatalog.GpuModel, gpuInfo, gpuInfo.Name, gpuLabelSuffix));
        }

        // Temperature — now bundled into each hardware's own Info model instead
        // of a centralized ITemperatureMonitorService. Kept at the same position
        // (end of the snapshot) to minimize output-order drift from before.
        foreach (var reading in cpuInfo.Temperatures)
        {
            readings.Add(BuildTemperatureMetric("CPU", reading, idSuffix: reading.SensorLabel));
        }
        foreach (var reading in diskInfo.Temperatures)
        {
            readings.Add(BuildTemperatureMetric("Disk", reading, idSuffix: reading.SensorLabel, labelSuffix: diskLabelSuffix));
        }
        foreach (var gpuInfo in gpuInfos)
        {
            if (!gpuInfo.IsAvailable) continue;
            var deviceTag = gpuInfo.IsIntegrated ? "Integrated" : "Dedicated";
            var gpuLabelSuffix = $" (GPU {gpuInfo.Index} - {deviceTag}: {gpuInfo.Name})";
            foreach (var reading in gpuInfo.Temperatures)
            {
                readings.Add(BuildTemperatureMetric(
                    "GPU", reading,
                    idSuffix: $"{gpuInfo.DeviceId}.{reading.SensorLabel}",
                    labelSuffix: gpuLabelSuffix,
                    gpuIndex: gpuInfo.Index,
                    gpuIsIntegrated: gpuInfo.IsIntegrated,
                    gpuDeviceId: gpuInfo.DeviceId));
            }
        }

        return readings;
    }

    private MetricReading BuildReading(
        MetricCatalogEntry entry, double rawValue, bool smooth = false, string? labelOverride = null)
    {
        var value = smooth ? Smooth(entry.Id, rawValue) : rawValue;
        return new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = labelOverride ?? entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = true,
            Value = Round(value)
        };
    }

    private MetricReading BuildPeakReading(MetricCatalogEntry entry, string sourceMetricId)
    {
        var (_, max) = _historyStore.GetCommittedRange(sourceMetricId);
        return new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = true,
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
        string? labelSuffix = null, int? gpuIndex = null, bool? gpuIsIntegrated = null,
        string? gpuDeviceId = null) => new()
    {
        Id = $"temp.{category}.{idSuffix}".ToLowerInvariant(),
        Category = category,
        Label = labelSuffix is null ? reading.SensorLabel : reading.SensorLabel + labelSuffix,
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
    MetricCatalogEntry entry, GpuInfo gpuInfo, string? text, string labelSuffix) => new()
    {
        Id = $"{entry.Id}.{gpuInfo.DeviceId}",
        Category = entry.Category,
        Label = entry.Label + labelSuffix,
        Kind = entry.Kind,
        Unit = entry.Unit,
        IsAvailable = text is not null,
        TextValue = text,
        GpuIndex = gpuInfo.Index,
        GpuIsIntegrated = gpuInfo.IsIntegrated,
        GpuDeviceId = gpuInfo.DeviceId
    };

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

    private static MetricReading BuildTextReading(MetricCatalogEntry entry, string? text) => new()
    {
        Id = entry.Id,
        Category = entry.Category,
        Label = entry.Label,
        Kind = entry.Kind,
        Unit = entry.Unit,
        IsAvailable = text is not null,
        TextValue = text
    };

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
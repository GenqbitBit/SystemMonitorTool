﻿﻿using System;
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
    private readonly ITemperatureMonitorService _temperature;
    private readonly IMotherboardMonitorService _motherboard;
    private readonly IGpuMonitorService _gpu; // Added to support teammate's GPU code

    private readonly Dictionary<string, Queue<double>> _smoothingWindows = new();
    private const int SmoothingWindow = 4;
    private const int DecimalPlaces = 2;

    public MetricsSnapshotProvider(
        ICpuMonitorService cpu,
        IMemoryMonitorService memory,
        IDiskMonitorService disk,
        INetworkMonitorService network,
        ITemperatureMonitorService temperature,
        IMotherboardMonitorService motherboard,
        IGpuMonitorService gpu)
    {
        _cpu = cpu;
        _memory = memory;
        _disk = disk;
        _network = network;
        _temperature = temperature;
        _motherboard = motherboard;
        _gpu = gpu;
    }

    public IReadOnlyList<MetricReading> GetSnapshot()
    {
        var readings = new List<MetricReading>();

        // CPU — identity rows first (emission order = display order).
        var cpuInfo = _cpu.GetCurrentUsage();
        readings.Add(BuildTextReading(MetricCatalog.CpuModel, cpuInfo.ModelName));
        readings.Add(BuildReading(MetricCatalog.CpuUsage, cpuInfo.UsagePercent, smooth: true));
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
        readings.Add(BuildReading(MetricCatalog.MemoryUsed, usedGB));
        readings.Add(BuildReading(MetricCatalog.MemoryTotal, totalGB));

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
        readings.Add(BuildReading(MetricCatalog.DiskWrite, diskInfo.WriteMBPerSec));

        // Network
        var networkInfo = _network.GetCurrentUsage();
        readings.Add(BuildReading(MetricCatalog.NetworkDownload, networkInfo.DownloadKBPerSec));
        readings.Add(BuildReading(MetricCatalog.NetworkUpload, networkInfo.UploadKBPerSec));

        // GPU — runtime-discovered, zero or more devices. Not catalog-driven for
        // Id/Label (same reasoning as Temperature): count and identity vary per
        // machine, so MetricCatalog.GpuUsage/etc. now only supply Category/Kind/
        // Unit/Label-stem plus design-time sample values, not the runtime Id.
        var gpuInfos = _gpu.GetCurrentUsage();
        foreach (var gpuInfo in gpuInfos)
        {
            if (!gpuInfo.IsAvailable) continue;

            var gpuUsedGB = gpuInfo.DedicatedMemoryUsedMb / 1024.0;
            var gpuTotalGB = gpuInfo.DedicatedMemoryTotalMb / 1024.0;
            var deviceTag = gpuInfo.IsIntegrated ? "Integrated" : "Dedicated";
            var gpuLabelSuffix = $" (GPU {gpuInfo.Index} - {deviceTag}: {gpuInfo.Name})";

            readings.Add(BuildGpuReading(MetricCatalog.GpuUsage, gpuInfo.Index, gpuInfo.UsagePercent, smooth: true, gpuLabelSuffix));
            readings.Add(BuildGpuReading(MetricCatalog.GpuMemoryUsed, gpuInfo.Index, gpuUsedGB, smooth: false, gpuLabelSuffix));
            readings.Add(BuildGpuReading(MetricCatalog.GpuMemoryTotal, gpuInfo.Index, gpuTotalGB, smooth: false, gpuLabelSuffix));
        }

        // Temperature — one MetricReading per sensor; runtime-discovered, so it
        // can't go through BuildReading/MetricCatalog like the sections above.
        var rawTemperatureReadings = _temperature.GetCurrentUsage();
        foreach (var reading in rawTemperatureReadings)
        {
            readings.Add(new MetricReading
            {
                Id = $"temp.{reading.Category}.{reading.SensorLabel}".ToLowerInvariant(),
                Category = reading.Category,
                Label = reading.SensorLabel,
                Kind = MetricKind.Temperature,
                Unit = "°C",
                IsAvailable = reading.IsAvailable,
                Value = Round(reading.TemperatureCelsius),
                Min = RoundNullable(reading.MinCelsius),
                Max = RoundNullable(reading.MaxCelsius),
                Average = RoundNullable(reading.AverageCelsius)
            });
        }

        // Power rows after the temperature loop so they sit at the bottom of
        // their sections (below Tctl/Tdie and GPU Hot Spot).
        readings.Add(BuildTextReading(MetricCatalog.CpuPackagePower, FormatWatts(cpuInfo.PackagePowerWatts)));
        readings.Add(BuildTextReading(MetricCatalog.GpuPackagePower, FormatWatts(gpuInfo.PowerUsage)));

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

    private MetricReading BuildGpuReading(
        MetricCatalogEntry entry, int gpuIndex, double rawValue, bool smooth, string labelSuffix)
    {
        var id = $"{entry.Id}.{gpuIndex}";
        var value = smooth ? Smooth(id, rawValue) : rawValue;

        return new MetricReading
        {
            Id = id,
            Category = entry.Category,
            Label = entry.Label + labelSuffix,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = true,
            Value = Round(value)
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
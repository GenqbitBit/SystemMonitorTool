using System;
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

        // CPU
        var cpuInfo = _cpu.GetCurrentUsage();
        readings.Add(BuildReading(MetricCatalog.CpuUsage, cpuInfo.UsagePercent, smooth: true));

        // Motherboard — identity rows are text; if detection failed, the section skips itself.
        var boardInfo = _motherboard.GetCurrentInfo();
        if (boardInfo is not null)
        {
            readings.Add(BuildTextReading(MetricCatalog.MotherboardModel, boardInfo.Model));
            readings.Add(BuildTextReading(MetricCatalog.MotherboardChipset, boardInfo.Chipset));

            readings.Add(new MetricReading
            {
                Id = MetricCatalog.MotherboardTemperature.Id,
                Category = MetricCatalog.MotherboardTemperature.Category,
                Label = MetricCatalog.MotherboardTemperature.Label,
                Kind = MetricCatalog.MotherboardTemperature.Kind,
                Unit = MetricCatalog.MotherboardTemperature.Unit,
                IsAvailable = boardInfo.TemperatureCelsius.HasValue,
                Value = Round(boardInfo.TemperatureCelsius ?? 0)
            });
        }

        // Memory
        var memInfo = _memory.GetCurrentUsage();
        var usedGB = memInfo.UsedMB / 1024.0;
        var totalGB = memInfo.TotalMB / 1024.0;

        readings.Add(BuildReading(MetricCatalog.MemoryUsage, memInfo.UsagePercent, smooth: true));
        readings.Add(BuildReading(MetricCatalog.MemoryUsed, usedGB));
        readings.Add(BuildReading(MetricCatalog.MemoryTotal, totalGB));

        // Disk
        var diskInfo = _disk.GetCurrentUsage();
        var diskLabelSuffix = $" ({diskInfo.DriveName})";

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

        // GPU (Teammate's addition)
        var gpuInfo = _gpu.GetCurrentUsage();
        var gpuUsedGB = gpuInfo.DedicatedMemoryUsedMb / 1024.0;
        var gpuTotalGB = gpuInfo.DedicatedMemoryTotalMb / 1024.0;
        var gpuLabelSuffix = $" ({gpuInfo.Name})";

        readings.Add(BuildReading(MetricCatalog.GpuUsage, gpuInfo.UsagePercent, smooth: true,
            labelOverride: MetricCatalog.GpuUsage.Label + gpuLabelSuffix));
        readings.Add(BuildReading(MetricCatalog.GpuMemoryUsed, gpuUsedGB));
        readings.Add(BuildReading(MetricCatalog.GpuMemoryTotal, gpuTotalGB));

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
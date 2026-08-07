using System.Collections.Generic;

namespace SystemMonitor.Domain.Models;

public sealed record MetricCatalogEntry(
    string Id,
    string Category,
    string Label,
    MetricKind Kind,
    string Unit,
    double SampleValue,
    double? SampleMin = null,
    double? SampleMax = null,
    double? SampleAverage = null,
    string? SampleText = null);

public static class MetricCatalog
{
    // CPU
    public static readonly MetricCatalogEntry CpuUsage =
        new("cpu.usage", "CPU", "Usage", MetricKind.Percentage, "%", 42.00);

    // Memory
    public static readonly MetricCatalogEntry MemoryUsage =
        new("memory.usage", "Memory", "Usage", MetricKind.Percentage, "%", 63.00);
    public static readonly MetricCatalogEntry MemoryUsed =
        new("memory.used", "Memory", "Used", MetricKind.DataSize, "GB", 10.20);
    public static readonly MetricCatalogEntry MemoryTotal =
        new("memory.total", "Memory", "Total", MetricKind.DataSize, "GB", 16.00);

    // Disk
    public static readonly MetricCatalogEntry DiskUsage =
        new("disk.usage", "Disk", "Usage", MetricKind.Percentage, "%", 55.00);
    public static readonly MetricCatalogEntry DiskUsed =
        new("disk.used", "Disk", "Used", MetricKind.DataSize, "GB", 256.00);
    public static readonly MetricCatalogEntry DiskTotal =
        new("disk.total", "Disk", "Total", MetricKind.DataSize, "GB", 512.00);
    public static readonly MetricCatalogEntry DiskRead =
        new("disk.read", "Disk", "Read", MetricKind.DataRate, "MB/s", 12.50);
    public static readonly MetricCatalogEntry DiskWrite =
        new("disk.write", "Disk", "Write", MetricKind.DataRate, "MB/s", 8.30);

    // Network
    public static readonly MetricCatalogEntry NetworkDownload =
        new("network.download", "Network", "Download", MetricKind.DataRate, "KB/s", 1200.00);
    public static readonly MetricCatalogEntry NetworkUpload =
        new("network.upload", "Network", "Upload", MetricKind.DataRate, "KB/s", 300.00);

    // Motherboard — identity rows are Text; temperature stays numeric.
    public static readonly MetricCatalogEntry MotherboardModel =
        new("motherboard.model", "Motherboard", "Model", MetricKind.Text, "", 0, SampleText: "TUF GAMING B550M");
    public static readonly MetricCatalogEntry MotherboardChipset =
        new("motherboard.chipset", "Motherboard", "Chipset", MetricKind.Text, "", 0, SampleText: "AMD B550");
    public static readonly MetricCatalogEntry MotherboardTemperature =
        new("motherboard.temperature", "Motherboard", "Temperature", MetricKind.Temperature, "°C", 42.50);

    // Temperature — illustrative only.
    public static readonly MetricCatalogEntry TempCpuCore =
        new("temp.cpu.core", "CPU", "Core Temp", MetricKind.Temperature, "°C", 55.00, 40.00, 70.00, 55.00);
    public static readonly MetricCatalogEntry TempGpuCore =
        new("temp.gpu.core", "GPU", "Core Temp", MetricKind.Temperature, "°C", 62.00, 45.00, 80.00, 60.00);
    public static readonly MetricCatalogEntry TempGpuHotSpot =
        new("temp.gpu.hotspot", "GPU", "Hot Spot", MetricKind.Temperature, "°C", 78.00, 50.00, 95.00, 75.00);

    public static IReadOnlyList<MetricCatalogEntry> All { get; } = new[]
    {
        CpuUsage,
        MemoryUsage, MemoryUsed, MemoryTotal,
        DiskUsage, DiskUsed, DiskTotal, DiskRead, DiskWrite,
        NetworkDownload, NetworkUpload,
        MotherboardModel, MotherboardChipset, MotherboardTemperature,
        TempCpuCore, TempGpuCore, TempGpuHotSpot
    };
}

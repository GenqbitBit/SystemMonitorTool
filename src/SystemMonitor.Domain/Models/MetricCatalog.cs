using System.Collections.Generic;

namespace SystemMonitor.Domain.Models;

/// <summary>
/// Static, compile-time known metadata for every metric with a fixed identity
/// (id/category/label/kind). Single source of truth for "shape" — consumed by
/// MetricsSnapshotProvider (which supplies live values) and by
/// CatalogDesignTimeMetricsSnapshotProvider (which supplies sample values for
/// the Avalonia previewer). Add/remove/rename a metric here and both update.
///
/// Not every metric fits this model — the Temperature entries below are
/// illustrative only. Real temperature sensors are discovered at runtime via
/// LibreHardwareMonitorLib and vary by machine (sensor count/labels aren't
/// knowable ahead of time), so the runtime provider does NOT read Id/Category/
/// Label from this catalog for temperature. These three entries exist purely
/// so the design-time preview has representative temperature rows to render.
/// </summary>
public sealed record MetricCatalogEntry(
    string Id,
    string Category,
    string Label,
    MetricKind Kind,
    string Unit,
    double SampleValue,
    double? SampleMin = null,
    double? SampleMax = null,
    double? SampleAverage = null);

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

    // Temperature — illustrative only, see remarks below.
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
        TempCpuCore, TempGpuCore, TempGpuHotSpot
    };
}
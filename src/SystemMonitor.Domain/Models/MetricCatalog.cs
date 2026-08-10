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
/// Label from this catalog for temperature. These entries exist purely so the
/// design-time preview has representative temperature rows to render.
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
    double? SampleAverage = null,
    string? SampleText = null,
    bool SampleIsPrimary = true);

public static class MetricCatalog
{
    // CPU
    public static readonly MetricCatalogEntry CpuModel =
        new("cpu.model", "CPU", "Model", MetricKind.Text, "", 0, SampleText: "AMD Ryzen 5 5600G with Radeon Graphics");
    public static readonly MetricCatalogEntry CpuUsage =
        new("cpu.usage", "CPU", "Usage", MetricKind.Percentage, "%", 42.00);
    public static readonly MetricCatalogEntry CpuClock =
        new("cpu.clock", "CPU", "Clock", MetricKind.Text, "", 0, SampleText: "4.39 GHz");
    public static readonly MetricCatalogEntry CpuCores =
        new("cpu.cores", "CPU", "Cores", MetricKind.Text, "", 0, SampleText: "6");
    public static readonly MetricCatalogEntry CpuThreads =
        new("cpu.threads", "CPU", "Threads", MetricKind.Text, "", 0, SampleText: "12");


    // Memory
    public static readonly MetricCatalogEntry MemoryName =
        new("memory.name", "Memory", "Name", MetricKind.Text, "", 0, SampleText: "KF432C16BB/16");
    public static readonly MetricCatalogEntry MemoryType =
        new("memory.type", "Memory", "Type", MetricKind.Text, "", 0, SampleText: "DDR4");
    public static readonly MetricCatalogEntry MemorySpeed =
        new("memory.speed", "Memory", "Speed", MetricKind.Text, "", 0, SampleText: "3200 MHz");
    public static readonly MetricCatalogEntry MemoryModules =
        new("memory.modules", "Memory", "Modules", MetricKind.Text, "", 0, SampleText: "2 x 8 GB");
    public static readonly MetricCatalogEntry MemoryManufacturer =
        new("memory.manufacturer", "Memory", "Manufacturer", MetricKind.Text, "", 0, SampleText: "Corsair");
    public static readonly MetricCatalogEntry MemoryUsage =
        new("memory.usage", "Memory", "Usage", MetricKind.Percentage, "%", 63.00);
    public static readonly MetricCatalogEntry MemoryUsed =
        new("memory.used", "Memory", "Used", MetricKind.DataSize, "GB", 10.20);
    public static readonly MetricCatalogEntry MemoryTotal =
        new("memory.total", "Memory", "Total", MetricKind.DataSize, "GB", 16.00);

    // Disk
    public static readonly MetricCatalogEntry DiskModel =
        new("disk.model", "Disk", "Model", MetricKind.Text, "", 0, SampleText: "KINGSTON SNV3S500G");
    public static readonly MetricCatalogEntry DiskType =
        new("disk.type", "Disk", "Type", MetricKind.Text, "", 0, SampleText: "SSD");
    public static readonly MetricCatalogEntry DiskBus =
        new("disk.bus", "Disk", "Bus", MetricKind.Text, "", 0, SampleText: "NVMe");
    public static readonly MetricCatalogEntry DiskFileSystem =
        new("disk.filesystem", "Disk", "File System", MetricKind.Text, "", 0, SampleText: "NTFS");
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

    // Motherboard — identity rows only; the temperature row was removed
    // because this machine's Super I/O channel never reports a real value.
    public static readonly MetricCatalogEntry MotherboardModel =
        new("motherboard.model", "Motherboard", "Model", MetricKind.Text, "", 0, SampleText: "TUF GAMING B550M");
    public static readonly MetricCatalogEntry MotherboardChipset =
        new("motherboard.chipset", "Motherboard", "Chipset", MetricKind.Text, "", 0, SampleText: "AMD B550");

    // Operating System — platform-neutral, driver-free. Identity first,
    // then live counts, mirroring the CPU section's order.
    public static readonly MetricCatalogEntry OsName =
        new("os.name", "OS", "OS", MetricKind.Text, "", 0, SampleText: "Windows 11");
    public static readonly MetricCatalogEntry OsVersion =
        new("os.version", "OS", "Version", MetricKind.Text, "", 0, SampleText: "Microsoft Windows NT 10.0.26100.0");
    public static readonly MetricCatalogEntry OsUptime =
        new("os.uptime", "OS", "Uptime", MetricKind.Text, "", 0, SampleText: "1d 4h 12m");
    public static readonly MetricCatalogEntry OsProcesses =
        new("os.processes", "OS", "Processes", MetricKind.Text, "", 0, SampleText: "182");
    public static readonly MetricCatalogEntry OsThreads =
        new("os.threads", "OS", "Threads", MetricKind.Text, "", 0, SampleText: "2450");
    public static readonly MetricCatalogEntry OsHandles =
        new("os.handles", "OS", "Handles", MetricKind.Text, "", 0, SampleText: "5,412,338");    

    // GPU
    public static readonly MetricCatalogEntry GpuUsage =
        new("gpu.usage", "GPU", "Usage", MetricKind.Percentage, "%", 37.00);
    public static readonly MetricCatalogEntry GpuMemoryUsed =
        new("gpu.memory.used", "GPU", "VRAM Used", MetricKind.DataSize, "GB", 2.00);
    public static readonly MetricCatalogEntry GpuMemoryTotal =
        new("gpu.memory.total", "GPU", "VRAM Total", MetricKind.DataSize, "GB", 8.00);

    // GPU — illustrative only, IS included in `All`. Two devices (dedicated +
    // integrated) so the previewer demonstrates multi-GPU rendering. Ids match
    // the ".{index}" scheme BuildGpuReading produces at runtime, but these
    // entries themselves are never read by runtime code.
    public static readonly MetricCatalogEntry GpuUsage0 =
        new("gpu.usage.0", "GPU", "Usage (GPU 0 - Dedicated: NVIDIA GeForce RTX 4060)", MetricKind.Percentage, "%", 37.00);
    public static readonly MetricCatalogEntry GpuMemoryUsed0 =
        new("gpu.memory.used.0", "GPU", "VRAM Used (GPU 0 - Dedicated: NVIDIA GeForce RTX 4060)", MetricKind.DataSize, "GB", 2.00);
    public static readonly MetricCatalogEntry GpuMemoryTotal0 =
        new("gpu.memory.total.0", "GPU", "VRAM Total (GPU 0 - Dedicated: NVIDIA GeForce RTX 4060)", MetricKind.DataSize, "GB", 8.00);

    public static readonly MetricCatalogEntry GpuUsage1 =
        new("gpu.usage.1", "GPU", "Usage (GPU 1 - Integrated: AMD Radeon Graphics)", MetricKind.Percentage, "%", 8.00);
    public static readonly MetricCatalogEntry GpuMemoryUsed1 =
        new("gpu.memory.used.1", "GPU", "VRAM Used (GPU 1 - Integrated: AMD Radeon Graphics)", MetricKind.DataSize, "GB", 0.30);
    public static readonly MetricCatalogEntry GpuMemoryTotal1 =
        new("gpu.memory.total.1", "GPU", "VRAM Total (GPU 1 - Integrated: AMD Radeon Graphics)", MetricKind.DataSize, "GB", 2.00);

    // Temperature — illustrative only. GPU entries deliberately mirror real
    // runtime shape: device-suffixed labels (same "(GPU {index} - Dedicated/
    // Integrated: {name})" pattern as the usage/VRAM rows above), and
    // SampleIsPrimary set the way WindowsTemperatureMonitorService actually
    // determines it — one primary "core" reading per device, everything else
    // on that device a sub-reading. The dedicated card shows the common case
    // (a real "GPU Core" sensor); the integrated entry shows the AMD-style
    // case where there's no "Core" sensor at all and a different one (here,
    // "VR SoC") ends up primary by fallback — so the previewer doesn't imply
    // every device has a Hot Spot/etc. sub-reading when some don't.
    public static readonly MetricCatalogEntry TempCpuCore =
        new("temp.cpu.core", "CPU", "Core Temp", MetricKind.Temperature, "°C", 55.00, 40.00, 70.00, 55.00);
    public static readonly MetricCatalogEntry TempGpuCoreDedicated =
        new("temp.gpu.core.0", "GPU", "GPU Core (GPU 0 - Dedicated: NVIDIA GeForce RTX 4060)",
            MetricKind.Temperature, "°C", 62.00, 45.00, 80.00, 60.00, SampleIsPrimary: true);
    public static readonly MetricCatalogEntry TempGpuSubDedicated =
        new("temp.gpu.hotspot.0", "GPU", "GPU Hot Spot (GPU 0 - Dedicated: NVIDIA GeForce RTX 4060)",
            MetricKind.Temperature, "°C", 78.00, 50.00, 95.00, 75.00, SampleIsPrimary: false);
    public static readonly MetricCatalogEntry TempGpuPrimaryIntegrated =
        new("temp.gpu.vrsoc.1", "GPU", "GPU VR SoC (GPU 1 - Integrated: AMD Radeon Graphics)",
            MetricKind.Temperature, "°C", 43.00, 35.00, 55.00, 44.00, SampleIsPrimary: true);

    public static IReadOnlyList<MetricCatalogEntry> All { get; } = new[]
    {
        CpuModel, CpuUsage, CpuClock, CpuCores, CpuThreads,   
        MemoryName, MemoryType, MemorySpeed, MemoryModules, MemoryManufacturer, MemoryUsage, MemoryUsed, MemoryTotal,
        DiskModel, DiskType, DiskBus, DiskFileSystem, DiskUsage, DiskUsed, DiskTotal, DiskRead, DiskWrite,
        NetworkDownload, NetworkUpload,
        MotherboardModel, MotherboardChipset,
        OsName, OsVersion, OsUptime, OsProcesses, OsThreads, OsHandles,
        GpuUsage0, GpuMemoryUsed0, GpuMemoryTotal0,
        GpuUsage1, GpuMemoryUsed1, GpuMemoryTotal1,
        TempCpuCore, TempGpuCoreDedicated, TempGpuSubDedicated, TempGpuPrimaryIntegrated
    };
}
using System;
using System.Collections.Generic;

namespace SystemMonitor.Domain.Models;

public class GpuInfo
{
    public bool IsAvailable { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;

    // Stable hardware identity (LUID-derived) — the join key for matching this
    // GPU's data across refreshes and across CpuInfo/MetricReading. NOT the
    // same thing as Index: Index can (in theory) shift if enumeration order
    // changes; DeviceId doesn't. Falls back to an index-based value only when
    // DXGI LUID resolution fails for this device (see WindowsGpuMonitorService).
    public string DeviceId { get; set; } = string.Empty;

    // Position in WMI enumeration order at startup. Display ordinal only now
    // ("GPU 0", "GPU 1" in labels) — DeviceId is the identity to key on.
    public int Index { get; set; }

    public bool IsIntegrated { get; set; }

    public double UsagePercent { get; set; }

    public double DedicatedMemoryUsedMb { get; set; }
    public double DedicatedMemoryTotalMb { get; set; }
    public double SharedMemoryUsedMb { get; set; }

    public List<TemperatureReading> Temperatures { get; set; } = new();

    public double? FanSpeed { get; set; }
    public double? PowerUsage { get; set; }
    public double? CoreClockMhz { get; set; }
    public double? MemoryClockMhz { get; set; }

    public string DriverVersion { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
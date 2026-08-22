using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsMemoryMonitorService : IMemoryMonitorService
{
    public MemoryInfo GetCurrentUsage()
    {
        return new MemoryInfo
        {
            TotalMB = 0,
            AvailableMB = 0,
            UsedMB = 0,
            UsagePercent = 0,
            PartNumber = "macOS (implementation pending)",
            Type = "Unknown",
            SpeedMhz = 0,
            ModuleConfig = "Unknown",
            Manufacturer = "Unknown"
        };
    }
}

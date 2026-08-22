using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux memory monitoring service (placeholder).
/// Full implementation will use /proc/meminfo.
/// Currently returns placeholder data.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxMemoryMonitorService : IMemoryMonitorService
{
    public MemoryInfo GetCurrentUsage()
    {
        // Placeholder implementation
        return new MemoryInfo
        {
            TotalMB = 0,
            AvailableMB = 0,
            UsedMB = 0,
            UsagePercent = 0,
            PartNumber = "Linux (implementation pending)",
            Type = "Unknown",
            SpeedMhz = 0,
            ModuleConfig = "Unknown",
            Manufacturer = "Unknown"
        };
    }
}

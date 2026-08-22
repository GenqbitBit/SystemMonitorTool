using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsDiskMonitorService : IDiskMonitorService
{
    public MacOsDiskMonitorService(string? mountPoint = null)
    {
    }

    public DiskInfo GetCurrentUsage()
    {
        return new DiskInfo
        {
            DriveName = "/",
            TotalGB = 0,
            UsedGB = 0,
            FreeGB = 0,
            UsagePercent = 0,
            Model = "macOS (implementation pending)",
            DiskType = "Unknown",
            BusType = "Unknown",
            FileSystem = "Unknown",
            ReadMBPerSec = 0,
            WriteMBPerSec = 0,
            Temperatures = new()
        };
    }
}

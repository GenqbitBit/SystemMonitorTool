using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux disk monitoring service (placeholder).
/// Full implementation will use /proc/diskstats and mount points.
/// Currently returns placeholder data.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxDiskMonitorService : IDiskMonitorService
{
    /// <summary>
    /// Initializes a new instance of the LinuxDiskMonitorService.
    /// </summary>
    /// <param name="mountPoint">The mount point (e.g., "/", "/home") to monitor. Defaults to root.</param>
    public LinuxDiskMonitorService(string? mountPoint = null)
    {
        // Placeholder - just store the mount point for now
    }

    public DiskInfo GetCurrentUsage()
    {
        // Placeholder implementation
        return new DiskInfo
        {
            DriveName = "/",
            TotalGB = 0,
            UsedGB = 0,
            FreeGB = 0,
            UsagePercent = 0,
            Model = "Linux (implementation pending)",
            DiskType = "Unknown",
            BusType = "Unknown",
            FileSystem = "Unknown",
            ReadMBPerSec = 0,
            WriteMBPerSec = 0,
            Temperatures = new()
        };
    }
}

using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux CPU monitoring service (placeholder).
/// Full implementation will use /proc/stat and /proc/cpuinfo.
/// Currently returns placeholder data.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxCpuMonitorService : ICpuMonitorService
{
    public CpuInfo GetCurrentUsage()
    {
        // Placeholder implementation
        return new CpuInfo
        {
            UsagePercent = 0,
            ModelName = "Linux (implementation pending)",
            ClockMhz = null,
            CoreCount = Environment.ProcessorCount,
            ThreadCount = Environment.ProcessorCount,
            PackagePowerWatts = null,
            Temperatures = new()
        };
    }
}

using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

/// <summary>
/// macOS CPU monitoring service (placeholder).
/// Full implementation will use sysctl and Activity Monitor APIs.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOsCpuMonitorService : ICpuMonitorService
{
    public CpuInfo GetCurrentUsage()
    {
        return new CpuInfo
        {
            UsagePercent = 0,
            ModelName = "macOS (implementation pending)",
            ClockMhz = null,
            CoreCount = Environment.ProcessorCount,
            ThreadCount = Environment.ProcessorCount,
            PackagePowerWatts = null,
            Temperatures = new()
        };
    }
}

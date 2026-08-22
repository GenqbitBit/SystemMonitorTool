using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsGpuMonitorService : IGpuMonitorService
{
    public IReadOnlyList<GpuInfo> GetCurrentUsage()
    {
        return new List<GpuInfo>();
    }
}

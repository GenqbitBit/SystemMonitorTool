using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux GPU monitoring service (placeholder).
/// Full implementation will use vulkan, glxinfo, or /sys/class/drm/.
/// Currently returns placeholder data.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxGpuMonitorService : IGpuMonitorService
{
    public IReadOnlyList<GpuInfo> GetCurrentUsage()
    {
        // Placeholder implementation - return empty list
        return new List<GpuInfo>();
    }
}

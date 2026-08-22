using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux motherboard monitoring service (placeholder).
/// Full implementation will use dmidecode or /sys/class/dmi/id/.
/// Currently returns placeholder data.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxMotherboardMonitorService : IMotherboardMonitorService
{
    public MotherboardInfo? GetCurrentInfo()
    {
        // Placeholder implementation - return null (data unavailable)
        return null;
    }
}

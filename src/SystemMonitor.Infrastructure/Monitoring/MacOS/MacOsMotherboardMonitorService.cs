using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsMotherboardMonitorService : IMotherboardMonitorService
{
    public MotherboardInfo? GetCurrentInfo()
    {
        return null;
    }
}

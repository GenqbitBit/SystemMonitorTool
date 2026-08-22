using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux motherboard identity from the unprivileged DMI sysfs interface.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxMotherboardMonitorService : IMotherboardMonitorService
{
    public MotherboardInfo? GetCurrentInfo()
    {
        var vendor = LinuxFileReader.ReadText("/sys/class/dmi/id/board_vendor");
        var name = LinuxFileReader.ReadText("/sys/class/dmi/id/board_name");
        var version = LinuxFileReader.ReadText("/sys/class/dmi/id/board_version");
        var product = LinuxFileReader.ReadText("/sys/class/dmi/id/product_name");
        if (vendor is null && name is null && version is null && product is null)
            return null;
        var model = string.Join(" ", new[] { vendor, name, version }
            .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        if (string.IsNullOrWhiteSpace(model)) model = product ?? "Unknown";
        return new MotherboardInfo(model, "Unknown", null);
    }
}

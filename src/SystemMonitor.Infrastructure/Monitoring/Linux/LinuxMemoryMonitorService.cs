using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux memory monitoring service backed by /proc/meminfo.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxMemoryMonitorService : IMemoryMonitorService
{
    public MemoryInfo GetCurrentUsage()
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in LinuxFileReader.ReadLines("/proc/meminfo"))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            var parts = line[(separator + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !double.TryParse(parts[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)) continue;

            values[key] = value / 1024d;
        }

        values.TryGetValue("MemTotal", out var totalMb);
        values.TryGetValue("MemAvailable", out var availableMb);
        var usedMb = Math.Max(0, totalMb - availableMb);

        return new MemoryInfo
        {
            TotalMB = totalMb,
            AvailableMB = availableMb,
            UsedMB = usedMb,
            UsagePercent = totalMb > 0 ? Math.Clamp(usedMb / totalMb * 100, 0, 100) : 0,
            PartNumber = string.Empty,
            Type = "Unknown",
            SpeedMhz = 0,
            ModuleConfig = "Unknown",
            Manufacturer = "Unknown"
        };
    }
}

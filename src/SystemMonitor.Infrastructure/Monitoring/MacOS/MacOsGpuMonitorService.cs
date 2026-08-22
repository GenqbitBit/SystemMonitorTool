using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Globalization;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsGpuMonitorService : IGpuMonitorService
{
    public IReadOnlyList<GpuInfo> GetCurrentUsage()
    {
        var devices = new List<GpuInfo>();
        using var document = MacOsCommandRunner.ParseJson(
            MacOsCommandRunner.Run("/usr/sbin/system_profiler", "SPDisplaysDataType", "-json"));
        if (document is null) return devices;

        foreach (var element in MacOsCommandRunner.Descendants(document.RootElement))
        {
            var name = MacOsCommandRunner.JsonString(element, "sppci_model", "_name");
            if (string.IsNullOrWhiteSpace(name) || !element.TryGetProperty("spdisplays_vendor", out _)) continue;

            var vendor = MacOsCommandRunner.JsonString(element, "spdisplays_vendor") ?? "Apple";
            var memory = ParseMemoryMb(MacOsCommandRunner.JsonString(element, "spdisplays_vram", "spdisplays_vram_shared"));
            devices.Add(new GpuInfo
            {
                IsAvailable = false,
                Name = name,
                Vendor = vendor,
                DeviceId = $"mac:{devices.Count}:{name}",
                Index = devices.Count,
                IsIntegrated = true,
                UsagePercent = 0,
                DedicatedMemoryUsedMb = 0,
                DedicatedMemoryTotalMb = memory,
                DriverVersion = "Unknown",
                Timestamp = DateTime.UtcNow
            });
        }
        return devices;
    }

    private static double ParseMemoryMb(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) return 0;
        return parts.Length > 1 && parts[1].StartsWith("GB", StringComparison.OrdinalIgnoreCase)
            ? amount * 1024 : amount;
    }
}

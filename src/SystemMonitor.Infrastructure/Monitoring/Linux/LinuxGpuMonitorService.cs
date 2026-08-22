using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux GPU discovery and driver-exposed telemetry from DRM sysfs.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxGpuMonitorService : IGpuMonitorService
{
    public IReadOnlyList<GpuInfo> GetCurrentUsage()
    {
        var devices = new List<GpuInfo>();
        var index = 0;
        foreach (var path in LinuxFileReader.GetDirectories("/sys/class/drm")
            .Where(path => Path.GetFileName(path).StartsWith("card", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(path).Skip(4).All(char.IsDigit)))
        {
            var devicePath = Path.Combine(path, "device");
            var vendorId = LinuxFileReader.ReadText(Path.Combine(devicePath, "vendor"));
            var vendor = vendorId switch
            {
                "0x10de" => "NVIDIA",
                "0x1002" => "AMD",
                "0x8086" => "Intel",
                _ => "Unknown"
            };
            var cardName = Path.GetFileName(path);
            var driver = LinuxFileReader.ReadText(Path.Combine(devicePath, "uevent"))?
                .Split('\n').FirstOrDefault(line => line.StartsWith("DRIVER=", StringComparison.Ordinal))?
                .Split('=', 2).ElementAtOrDefault(1) ?? string.Empty;
            var busyPath = Path.Combine(devicePath, "gpu_busy_percent");
            var usedPath = Path.Combine(devicePath, "mem_info_vram_used");
            var totalPath = Path.Combine(devicePath, "mem_info_vram_total");
            var hasUsage = LinuxFileReader.TryReadDouble(busyPath, out var usage);
            var hasUsed = LinuxFileReader.TryReadDouble(usedPath, out var used);
            var hasTotal = LinuxFileReader.TryReadDouble(totalPath, out var total);
            devices.Add(new GpuInfo
            {
                IsAvailable = hasUsage || hasUsed || hasTotal,
                Name = $"{vendor} {cardName}",
                Vendor = vendor,
                DeviceId = $"drm:{cardName}",
                Index = index++,
                IsIntegrated = false,
                UsagePercent = Math.Clamp(usage, 0, 100),
                DedicatedMemoryUsedMb = used / 1024 / 1024,
                DedicatedMemoryTotalMb = total / 1024 / 1024,
                DriverVersion = driver,
                Timestamp = DateTime.UtcNow
            });
        }
        return devices;
    }
}

using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux CPU monitoring service backed by /proc and sysfs.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxCpuMonitorService : ICpuMonitorService
{
    private readonly object _gate = new();
    private (long Idle, long Total)? _previous;

    public CpuInfo GetCurrentUsage()
    {
        var (idle, total) = ReadCpuTotals();
        double usage = 0;
        lock (_gate)
        {
            if (_previous is { } previous)
            {
                var totalDelta = total - previous.Total;
                var idleDelta = idle - previous.Idle;
                if (totalDelta > 0)
                    usage = Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
            }
            _previous = (idle, total);
        }

        var cpuInfo = LinuxFileReader.ReadLines("/proc/cpuinfo");
        var modelName = cpuInfo.FirstOrDefault(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2).ElementAtOrDefault(1)?.Trim() ?? "Unknown";
        var threadCount = cpuInfo.Count(line => line.StartsWith("processor", StringComparison.OrdinalIgnoreCase));
        if (threadCount == 0) threadCount = Environment.ProcessorCount;

        var physicalIds = cpuInfo.Where(line => line.StartsWith("physical id", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split(':', 2).ElementAtOrDefault(1)?.Trim()).ToArray();
        var coreIds = cpuInfo.Where(line => line.StartsWith("core id", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split(':', 2).ElementAtOrDefault(1)?.Trim()).ToArray();
        var physicalCoreCount = physicalIds.Length == coreIds.Length && physicalIds.Length > 0
            ? physicalIds.Zip(coreIds).Select(pair => $"{pair.First}:{pair.Second}").Distinct().Count()
            : coreIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Count();
        var coreCount = physicalCoreCount > 0 ? physicalCoreCount : threadCount;
        double? clockMhz = null;
        var frequencyPath = LinuxFileReader.GetDirectories("/sys/devices/system/cpu/cpu0/cpufreq").Length > 0
            ? "/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq" : string.Empty;
        if (!string.IsNullOrEmpty(frequencyPath) && LinuxFileReader.TryReadDouble(frequencyPath, out var frequency))
            clockMhz = frequency / 1000d;

        return new CpuInfo
        {
            UsagePercent = Math.Round(usage, 2),
            ModelName = modelName,
            ClockMhz = clockMhz,
            CoreCount = coreCount,
            ThreadCount = threadCount,
            PackagePowerWatts = null,
            Temperatures = new()
        };
    }

    private static (long Idle, long Total) ReadCpuTotals()
    {
        var line = LinuxFileReader.ReadLines("/proc/stat")
            .FirstOrDefault(value => value.StartsWith("cpu ", StringComparison.Ordinal));
        var values = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(value => long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : 0)
            .ToArray() ?? Array.Empty<long>();
        var idle = values.ElementAtOrDefault(3) + values.ElementAtOrDefault(4);
        return (idle, values.Sum());
    }
}

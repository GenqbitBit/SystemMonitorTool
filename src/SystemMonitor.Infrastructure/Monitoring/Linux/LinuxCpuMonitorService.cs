using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux CPU monitoring service backed by /proc and sysfs.
/// Static identity is parsed once; only the usage counters and current
/// frequency are read per tick.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxCpuMonitorService : ICpuMonitorService
{
    private readonly object _gate = new();
    private (long Idle, long Total)? _previous;

    private string _modelName = "Unknown";
    private int _coreCount;
    private int _threadCount;
    private bool _identityLoaded;

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

        EnsureStaticIdentity();

        double? clockMhz = null;
        if (LinuxFileReader.TryReadDouble(
                "/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq", out var frequency))
        {
            clockMhz = frequency / 1000d;
        }

        return new CpuInfo
        {
            UsagePercent = Math.Round(usage, 2),
            ModelName = _modelName,
            ClockMhz = clockMhz,
            CoreCount = _coreCount,
            ThreadCount = _threadCount,
            PackagePowerWatts = null,
            Temperatures = new()
        };
    }

    // /proc/cpuinfo can be hundreds of lines and is static for the session.
    private void EnsureStaticIdentity()
    {
        if (_identityLoaded) return;
        _identityLoaded = true;

        var cpuInfo = LinuxFileReader.ReadLines("/proc/cpuinfo");

        foreach (var line in cpuInfo)
        {
            if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
            {
                var separator = line.IndexOf(':');
                _modelName = separator >= 0 ? line[(separator + 1)..].Trim() : "Unknown";
                break;
            }
        }

        var threadCount = 0;
        foreach (var line in cpuInfo)
        {
            if (line.StartsWith("processor", StringComparison.OrdinalIgnoreCase))
                threadCount++;
        }
        if (threadCount == 0) threadCount = Environment.ProcessorCount;
        _threadCount = threadCount;

        // Physical cores = distinct (physical id, core id) pairs.
        var pairs = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        string? physicalId = null;
        string? coreId = null;
        foreach (var line in cpuInfo)
        {
            if (line.StartsWith("physical id", StringComparison.OrdinalIgnoreCase))
            {
                var separator = line.IndexOf(':');
                physicalId = separator >= 0 ? line[(separator + 1)..].Trim() : null;
            }
            else if (line.StartsWith("core id", StringComparison.OrdinalIgnoreCase))
            {
                var separator = line.IndexOf(':');
                coreId = separator >= 0 ? line[(separator + 1)..].Trim() : null;
                if (coreId is not null)
                    pairs.Add($"{physicalId}:{coreId}");
            }
        }
        _coreCount = pairs.Count > 0 ? pairs.Count : _threadCount;
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

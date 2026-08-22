using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

/// <summary>
/// macOS CPU monitoring service backed by sysctl.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOsCpuMonitorService : ICpuMonitorService
{
    private readonly object _gate = new();
    private long[]? _previousCpuTimes;

    public CpuInfo GetCurrentUsage()
    {
        var cpuTimes = ReadCpuTimes();
        var usage = 0d;
        lock (_gate)
        {
            if (_previousCpuTimes is { Length: > 0 } previous && cpuTimes.Length == previous.Length)
            {
                var totalDelta = cpuTimes.Sum() - previous.Sum();
                var idleDelta = GetIdleValue(cpuTimes) - GetIdleValue(previous);
                if (totalDelta > 0)
                    usage = Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
            }
            _previousCpuTimes = cpuTimes;
        }

        var model = MacOsCommandRunner.ReadSysctl("hw.model");
        var coreCount = ReadInt("hw.physicalcpu", Environment.ProcessorCount);
        var threadCount = ReadInt("hw.ncpu", Environment.ProcessorCount);
        double? clockMhz = null;
        var frequency = MacOsCommandRunner.ReadSysctl("hw.cpufrequency");
        if (MacOsCommandRunner.TryReadDouble(frequency, out var frequencyHz) && frequencyHz > 0)
            clockMhz = frequencyHz / 1_000_000d;

        return new CpuInfo
        {
            UsagePercent = Math.Round(usage, 2),
            ModelName = string.IsNullOrWhiteSpace(model) ? "Unknown" : model,
            ClockMhz = clockMhz,
            CoreCount = coreCount,
            ThreadCount = threadCount,
            PackagePowerWatts = null,
            Temperatures = new()
        };
    }

    private static long[] ReadCpuTimes()
    {
        var output = MacOsCommandRunner.ReadSysctl("kern.cp_time");
        return output.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => MacOsCommandRunner.TryReadLong(value, out var parsed) && parsed >= 0 ? parsed : 0)
            .ToArray();
    }

    private static long GetIdleValue(long[] cpuTimes)
    {
        if (cpuTimes.Length == 0) return 0;
        var idleIndex = cpuTimes.Length >= 4 ? 3 : cpuTimes.Length - 1;
        return cpuTimes[Math.Clamp(idleIndex, 0, cpuTimes.Length - 1)];
    }

    private static int ReadInt(string name, int fallback)
    {
        return int.TryParse(MacOsCommandRunner.ReadSysctl(name), out var value) && value > 0
            ? value : fallback;
    }
}

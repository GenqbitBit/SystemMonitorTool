using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsMemoryMonitorService : IMemoryMonitorService
{
    public MemoryInfo GetCurrentUsage()
    {
        var totalBytes = ReadLong("hw.memsize");
        var pageSize = ReadLong("hw.pagesize", 4096);
        var pages = MacOsCommandRunner.ParseVmStat(
            MacOsCommandRunner.Run("/usr/bin/vm_stat"));
        var availablePages = GetPages(pages, "Pages free")
            + GetPages(pages, "Pages inactive")
            + GetPages(pages, "Pages speculative");
        var availableBytes = availablePages * pageSize;
        var usedBytes = Math.Max(0, totalBytes - availableBytes);
        var totalMb = totalBytes / 1024d / 1024;
        var availableMb = availableBytes / 1024d / 1024;
        var usedMb = usedBytes / 1024d / 1024;

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

    private static long ReadLong(string name, long fallback = 0) =>
        MacOsCommandRunner.TryReadLong(MacOsCommandRunner.ReadSysctl(name), out var value) && value > 0
            ? value : fallback;

    private static long GetPages(IReadOnlyDictionary<string, long> pages, string name) =>
        pages.TryGetValue(name, out var value) ? value : 0;
}

using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsDiskMonitorService : IDiskMonitorService
{
    private readonly string _mountPoint;

    public MacOsDiskMonitorService(string? mountPoint = null)
    {
        _mountPoint = string.IsNullOrWhiteSpace(mountPoint) ? "/" : mountPoint;
    }

    public DiskInfo GetCurrentUsage()
    {
        DriveInfo? drive = null;
        try { drive = new DriveInfo(_mountPoint); } catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { }
        var totalBytes = 0L;
        var freeBytes = 0L;
        var fileSystem = "Unknown";
        var isReady = false;
        try { isReady = drive?.IsReady == true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (drive is not null && isReady)
        {
            try
            {
                totalBytes = drive.TotalSize;
                freeBytes = drive.AvailableFreeSpace;
                fileSystem = drive.DriveFormat;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        var usedBytes = Math.Max(0, totalBytes - freeBytes);
        var identity = ReadIdentity();
        var (readRate, writeRate) = ReadRates();
        return new DiskInfo
        {
            DriveName = _mountPoint,
            TotalGB = ToGb(totalBytes),
            UsedGB = ToGb(usedBytes),
            FreeGB = ToGb(freeBytes),
            UsagePercent = totalBytes > 0 ? Math.Clamp(usedBytes * 100d / totalBytes, 0, 100) : 0,
            Model = identity.Model,
            DiskType = identity.DiskType,
            BusType = identity.BusType,
            FileSystem = fileSystem,
            ReadMBPerSec = readRate,
            WriteMBPerSec = writeRate,
            Temperatures = new()
        };
    }

    private (double ReadRate, double WriteRate) ReadRates()
    {
        var output = MacOsCommandRunner.Run("/usr/sbin/iostat", "-Id", "-c", "1");
        var values = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 3 && parts.Skip(1).All(value => MacOsCommandRunner.TryReadDouble(value, out _)))
            .Select(parts => parts.Skip(1).Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).Last())
            .ToArray();
        var rate = values.Sum();
        return (Math.Max(0, rate), 0);
    }

    private (string Model, string DiskType, string BusType) ReadIdentity()
    {
        var source = MacOsCommandRunner.Run("/bin/df", "-P", _mountPoint)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1).Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(source)) return ("Unknown", "Unknown", "Unknown");

        var plist = MacOsCommandRunner.ParsePlist(MacOsCommandRunner.Run("/usr/sbin/diskutil", "info", "-plist", source));
        var model = plist.TryGetValue("MediaName", out var mediaName) ? mediaName : "Unknown";
        var bus = plist.TryGetValue("Protocol", out var protocol) ? protocol : "Unknown";
        var diskType = plist.TryGetValue("SolidState", out var solidState) && bool.TryParse(solidState, out var isSolidState)
            ? (isSolidState ? "SSD" : "HDD") : "Unknown";
        return (model, diskType, bus);
    }

    private static double ToGb(long bytes) => bytes / 1024d / 1024 / 1024;
}

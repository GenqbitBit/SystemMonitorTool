using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsDiskMonitorService : IDiskMonitorService
{
    private static readonly TimeSpan RateRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly string _mountPoint;
    private readonly object _rateGate = new();
    private (string Model, string DiskType, string BusType)? _identity;
    private (double ReadRate, double WriteRate) _rates;
    private DateTime _ratesUtc = DateTime.MinValue;
    private bool _rateRefreshRunning;

    public MacOsDiskMonitorService(string? mountPoint = null)
    {
        _mountPoint = string.IsNullOrWhiteSpace(mountPoint) ? "/" : mountPoint;
    }

    public DiskInfo GetCurrentUsage()
    {
        DriveInfo? drive = null;
        try { drive = new DriveInfo(_mountPoint); }
        catch (Exception ex) when (ex is ArgumentException or System.IO.IOException or NotSupportedException) { }

        var totalBytes = 0L;
        var freeBytes = 0L;
        var fileSystem = "Unknown";
        var isReady = false;
        try { isReady = drive?.IsReady == true; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }

        if (drive is not null && isReady)
        {
            try
            {
                totalBytes = drive.TotalSize;
                freeBytes = drive.AvailableFreeSpace;
                fileSystem = drive.DriveFormat;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
        }

        var usedBytes = Math.Max(0, totalBytes - freeBytes);
        var identity = _identity ??= ReadIdentity();
        var (readRate, writeRate) = GetRates();
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

    // iostat -Id -c 1 blocks for about a second. Never run it on the polling
    // thread: serve the last measurement and refresh in the background.
    private (double ReadRate, double WriteRate) GetRates()
    {
        lock (_rateGate)
        {
            if (DateTime.UtcNow - _ratesUtc < RateRefreshInterval || _rateRefreshRunning)
                return _rates;

            _rateRefreshRunning = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var measured = MeasureRates();
                lock (_rateGate)
                {
                    _rates = measured;
                    _ratesUtc = DateTime.UtcNow;
                }
            }
            catch
            {
                // Keep the last known rate.
            }
            finally
            {
                lock (_rateGate)
                    _rateRefreshRunning = false;
            }
        });

        lock (_rateGate)
            return _rates;
    }

    private static (double ReadRate, double WriteRate) MeasureRates()
    {
        var output = MacOsCommandRunner.Run("/usr/sbin/iostat", "-Id", "-c", "1");
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var rate = 0d;

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            if (!MacOsCommandRunner.TryReadDouble(parts[^1], out var value)) continue;
            rate += value;
        }

        return (Math.Max(0, rate), 0);
    }

    private (string Model, string DiskType, string BusType) ReadIdentity()
    {
        var source = MacOsCommandRunner.Run("/bin/df", "-P", _mountPoint)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(source)) return ("Unknown", "Unknown", "Unknown");

        var plist = MacOsCommandRunner.ParsePlist(
            MacOsCommandRunner.Run("/usr/sbin/diskutil", "info", "-plist", source));
        var model = plist.TryGetValue("MediaName", out var mediaName) ? mediaName : "Unknown";
        var bus = plist.TryGetValue("Protocol", out var protocol) ? protocol : "Unknown";
        var diskType = plist.TryGetValue("SolidState", out var solidState) && bool.TryParse(solidState, out var isSolidState)
            ? (isSolidState ? "SSD" : "HDD") : "Unknown";
        return (model, diskType, bus);
    }

    private static double ToGb(long bytes) => bytes / 1024d / 1024 / 1024;
}

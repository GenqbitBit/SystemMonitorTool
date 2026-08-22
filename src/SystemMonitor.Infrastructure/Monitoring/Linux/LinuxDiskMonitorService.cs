using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux disk monitoring service backed by DriveInfo, /proc/diskstats, and sysfs.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxDiskMonitorService : IDiskMonitorService
{
    private readonly string _mountPoint;
    private readonly object _gate = new();
    private (long ReadSectors, long WriteSectors, DateTime Timestamp)? _previous;

    /// <summary>
    /// Initializes a new instance of the LinuxDiskMonitorService.
    /// </summary>
    /// <param name="mountPoint">The mount point (e.g., "/", "/home") to monitor. Defaults to root.</param>
    public LinuxDiskMonitorService(string? mountPoint = null)
    {
        _mountPoint = string.IsNullOrWhiteSpace(mountPoint) ? "/" : mountPoint;
    }

    public DiskInfo GetCurrentUsage()
    {
        DriveInfo? drive;
        try { drive = new DriveInfo(_mountPoint); }
        catch (ArgumentException) { drive = null; }
        catch (IOException) { drive = null; }
        catch (NotSupportedException) { drive = null; }
        var ready = false;
        try { ready = drive?.IsReady == true; }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        long totalBytes = 0;
        long freeBytes = 0;
        string fileSystem = "Unknown";
        if (ready)
        {
            try
            {
                totalBytes = drive!.TotalSize;
                freeBytes = drive.AvailableFreeSpace;
                fileSystem = drive.DriveFormat;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var usedBytes = Math.Max(0, totalBytes - freeBytes);
        var source = ResolveMountSource(_mountPoint);
        var deviceName = source is not null && source.StartsWith("/dev/", StringComparison.Ordinal)
            ? Path.GetFileName(source) : null;
        var stats = deviceName is null ? null : ReadDiskStats(deviceName);
        var (readRate, writeRate) = CalculateRates(stats);
        var model = deviceName is null ? null : LinuxFileReader.ReadText($"/sys/block/{GetBaseDevice(deviceName)}/device/model");
        var rotational = deviceName is null ? null : LinuxFileReader.ReadText($"/sys/block/{GetBaseDevice(deviceName)}/queue/rotational");
        var diskType = rotational == "1" ? "HDD" : rotational == "0" ? "SSD" : "Unknown";
        var busType = deviceName?.StartsWith("nvme", StringComparison.OrdinalIgnoreCase) == true ? "NVMe" : "Unknown";

        return new DiskInfo
        {
            DriveName = _mountPoint,
            TotalGB = ToGb(totalBytes),
            UsedGB = ToGb(usedBytes),
            FreeGB = ToGb(freeBytes),
            UsagePercent = totalBytes > 0 ? Math.Clamp(usedBytes * 100d / totalBytes, 0, 100) : 0,
            Model = model ?? "Unknown",
            DiskType = diskType,
            BusType = busType,
            FileSystem = fileSystem,
            ReadMBPerSec = readRate,
            WriteMBPerSec = writeRate,
            Temperatures = new()
        };
    }

    private (double ReadRate, double WriteRate) CalculateRates((long ReadSectors, long WriteSectors)? current)
    {
        if (current is null) return (0, 0);
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            var result = (0d, 0d);
            if (_previous is { } previous)
            {
                var seconds = Math.Max(0.001, (now - previous.Timestamp).TotalSeconds);
                result = ((current.Value.ReadSectors - previous.ReadSectors) * 512d / 1024 / 1024 / seconds,
                    (current.Value.WriteSectors - previous.WriteSectors) * 512d / 1024 / 1024 / seconds);
            }
            _previous = (current.Value.ReadSectors, current.Value.WriteSectors, now);
            return (Math.Max(0, result.Item1), Math.Max(0, result.Item2));
        }
    }

    private static (long ReadSectors, long WriteSectors)? ReadDiskStats(string deviceName)
    {
        foreach (var line in LinuxFileReader.ReadLines("/proc/diskstats"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 11 || !string.Equals(parts[2], deviceName, StringComparison.Ordinal)) continue;
            if (long.TryParse(parts[5], out var read) && long.TryParse(parts[9], out var written))
                return (read, written);
        }
        return null;
    }

    private static string? ResolveMountSource(string mountPoint)
    {
        string normalized;
        try { normalized = Path.GetFullPath(mountPoint); }
        catch (ArgumentException) { return null; }
        catch (IOException) { return null; }
        string? bestSource = null;
        var bestLength = -1;
        foreach (var line in LinuxFileReader.ReadLines("/proc/self/mounts"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var mountedPath = parts[1].Replace("\\040", " ").Replace("\\011", "\t");
            if (normalized == mountedPath || (normalized.StartsWith(mountedPath + "/", StringComparison.Ordinal) && mountedPath.Length > bestLength))
            {
                bestSource = parts[0];
                bestLength = mountedPath.Length;
            }
        }
        return bestSource;
    }

    private static string GetBaseDevice(string deviceName)
    {
        if (deviceName.StartsWith("nvme", StringComparison.OrdinalIgnoreCase))
        {
            var partitionIndex = deviceName.IndexOf('p', 4);
            return partitionIndex > 0 ? deviceName[..partitionIndex] : deviceName;
        }
        var end = deviceName.Length;
        while (end > 0 && char.IsDigit(deviceName[end - 1])) end--;
        return end > 0 ? deviceName[..end] : deviceName;
    }

    private static double ToGb(long bytes) => bytes / 1024d / 1024 / 1024;
}

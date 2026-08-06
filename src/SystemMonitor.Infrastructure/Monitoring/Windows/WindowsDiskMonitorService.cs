using System;
using System.Diagnostics;
using System.IO;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsDiskMonitorService : IDiskMonitorService
{
    private readonly string _driveName;
    private readonly PerformanceCounter _readCounter;
    private readonly PerformanceCounter _writeCounter;

    // Defaults to the system drive; pass a different letter later if you want to monitor multiple drives
    public WindowsDiskMonitorService(string driveName = "C:\\")
    {
        _driveName = driveName;

        // "_Total" aggregates across all physical disks — swap to a specific
        // instance name (e.g. "0 C:") later if you want per-drive I/O specifically
        _readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        _writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        _readCounter.NextValue();  // warm-up, same reasoning as CpuMonitorService
        _writeCounter.NextValue();
    }

    public DiskInfo GetCurrentUsage()
    {
        var drive = new DriveInfo(_driveName);

        const double bytesPerGB = 1024L * 1024 * 1024;
        const double bytesPerMB = 1024 * 1024;

        var totalGB = drive.TotalSize / bytesPerGB;
        var freeGB = drive.TotalFreeSpace / bytesPerGB;
        var usedGB = totalGB - freeGB;
        var usagePercent = usedGB / totalGB * 100;

        return new DiskInfo
        {
            DriveName = drive.Name,
            TotalGB = totalGB,
            FreeGB = freeGB,
            UsedGB = usedGB,
            UsagePercent = usagePercent,
            ReadMBPerSec = _readCounter.NextValue() / bytesPerMB,
            WriteMBPerSec = _writeCounter.NextValue() / bytesPerMB
        };
    }
}
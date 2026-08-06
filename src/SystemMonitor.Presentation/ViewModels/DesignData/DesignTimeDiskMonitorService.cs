using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeDiskMonitorService : IDiskMonitorService
{
    public DiskInfo GetCurrentUsage() => new DiskInfo
    {
        DriveName = "C:\\",
        TotalGB = 476,
        FreeGB = 210,
        UsedGB = 266,
        UsagePercent = 55.9,
        ReadMBPerSec = 12.4,
        WriteMBPerSec = 3.7
    };
}
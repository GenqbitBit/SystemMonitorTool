using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeMemoryMonitorService : IMemoryMonitorService
{
    public MemoryInfo GetCurrentUsage() => new MemoryInfo
    {
        TotalMB = 16384,
        AvailableMB = 6210,
        UsedMB = 10174,
        UsagePercent = 62.1
    };
}
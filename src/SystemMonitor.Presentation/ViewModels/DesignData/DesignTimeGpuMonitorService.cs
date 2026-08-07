using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeGpuMonitorService : IGpuMonitorService
{
    public GpuInfo GetCurrentUsage() => new GpuInfo
    {
        Name = "Design-Time GPU",
        Vendor = "NVIDIA",
        UsagePercent = 37,
        DedicatedMemoryUsedMb = 2048,
        DedicatedMemoryTotalMb = 8192,
        IsIntegrated = false
    };
}

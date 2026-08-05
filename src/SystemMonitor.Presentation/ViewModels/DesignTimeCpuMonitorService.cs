using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeCpuMonitorService : ICpuMonitorService
{
    public CpuInfo GetCurrentUsage() => new CpuInfo { UsagePercent = 42 };
}
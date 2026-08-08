using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IGpuMonitorService
{
    GpuInfo GetCurrentUsage();
}

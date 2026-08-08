using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface ICpuMonitorService
{
    CpuInfo GetCurrentUsage();
}
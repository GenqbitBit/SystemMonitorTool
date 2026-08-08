using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IMemoryMonitorService
{
    MemoryInfo GetCurrentUsage();
}
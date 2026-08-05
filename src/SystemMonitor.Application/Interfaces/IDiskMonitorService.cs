using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IDiskMonitorService
{
    DiskInfo GetCurrentUsage();
}
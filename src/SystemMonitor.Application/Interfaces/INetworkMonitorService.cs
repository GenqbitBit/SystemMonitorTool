using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface INetworkMonitorService
{
    NetworkInfo GetCurrentUsage();
}
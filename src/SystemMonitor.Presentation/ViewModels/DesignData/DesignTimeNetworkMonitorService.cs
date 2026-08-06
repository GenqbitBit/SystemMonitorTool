using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

internal class DesignTimeNetworkMonitorService : INetworkMonitorService
{
    public NetworkInfo GetCurrentUsage() => new NetworkInfo
    {
        DownloadKBPerSec = 842.5,
        UploadKBPerSec = 63.2
    };
}
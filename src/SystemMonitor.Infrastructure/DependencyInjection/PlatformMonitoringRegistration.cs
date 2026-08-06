using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Monitoring.Windows;

namespace SystemMonitor.Infrastructure;

public static class PlatformMonitoringRegistration
{
    public static IServiceCollection AddPlatformMonitoringServices(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ICpuMonitorService, WindowsCpuMonitorService>();
            services.AddSingleton<IMemoryMonitorService, WindowsMemoryMonitorService>();
            services.AddSingleton<IDiskMonitorService, WindowsDiskMonitorService>();
            services.AddSingleton<INetworkMonitorService, WindowsNetworkMonitorService>();
        }
        else if (OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Linux support is not implemented yet.");
        }
        else if (OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("macOS support is not implemented yet.");
        }
        else
        {
            throw new PlatformNotSupportedException("This operating system is not supported.");
        }

        return services;
    }
}
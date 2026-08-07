using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Monitoring.Windows;
using SystemMonitor.Application.UseCases;

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
            services.AddSingleton<ITemperatureMonitorService, WindowsTemperatureMonitorService>();
            services.AddSingleton<IMotherboardMonitorService, WindowsMotherboardMonitorService>();
            services.AddSingleton<IMetricsSnapshotProvider, MetricsSnapshotProvider>();
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
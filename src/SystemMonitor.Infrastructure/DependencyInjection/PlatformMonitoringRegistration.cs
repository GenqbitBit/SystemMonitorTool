using System;
using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Monitoring.Windows;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;
using SystemMonitor.Infrastructure.Persistence;
namespace SystemMonitor.Infrastructure;

public static class PlatformMonitoringRegistration
{
    public static IServiceCollection AddPlatformMonitoringServices(this IServiceCollection services)
    {
        services.AddSingleton<IOsMonitorService, DotNetOsMonitorService>();

        // Cross-platform — Microsoft.Data.Sqlite works identically on every
        // OS, so this doesn't belong inside the Windows-only branch below.
        services.AddSingleton<IMetricHistoryPersistenceService, SqliteMetricHistoryPersistenceService>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ICpuMonitorService, WindowsCpuMonitorService>();
            services.AddSingleton<IMemoryMonitorService, WindowsMemoryMonitorService>();
            services.AddSingleton<IDiskMonitorService, WindowsDiskMonitorService>();
            services.AddSingleton<INetworkMonitorService, WindowsNetworkMonitorService>();
            services.AddSingleton<IMotherboardMonitorService, WindowsMotherboardMonitorService>();
            services.AddSingleton<IGpuMonitorService, WindowsGpuMonitorService>();
            services.AddSingleton<IMetricsSnapshotProvider, MetricsSnapshotProvider>();
            services.AddSingleton<IMetricHistoryStore>(_ => new MetricHistoryStore(TimeSpan.FromSeconds(60)));
            
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
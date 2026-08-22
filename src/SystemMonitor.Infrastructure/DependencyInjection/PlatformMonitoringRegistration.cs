using System;
using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;
using SystemMonitor.Infrastructure.Persistence;
using SystemMonitor.Domain.AsciiArt;
using SystemMonitor.Application.AsciiArt;

#pragma warning disable CA1416 // Validate platform compatibility - Platform-specific implementations are invoked conditionally at runtime

namespace SystemMonitor.Infrastructure.DependencyInjection;

/// <summary>
/// Registers platform-specific monitoring services based on the current operating system.
/// Each platform has its own implementation, with graceful degradation for unavailable features.
/// </summary>
public static class PlatformMonitoringRegistration
{
    public static IServiceCollection AddPlatformMonitoringServices(this IServiceCollection services)
    {
        // Register cross-platform services (work on all OS)
        services.AddSingleton<IOsMonitorService, DotNetOsMonitorService>();
        services.AddSingleton<IMetricHistoryPersistenceService, SqliteMetricHistoryPersistenceService>();
        services.AddSingleton<IEventLogService, SqliteEventLogService>();
        services.AddSingleton<IAsciiArtConverter, AsciiArtConverter>();

        services.AddSingleton<IThresholdMonitorService>(sp =>
            new ThresholdMonitorService(
                sp.GetRequiredService<IEventLogService>(),
                new[]
                {
                    new MetricThreshold("cpu.usage", WarningValue: 80, CriticalValue: 90),
                    new MetricThreshold("memory.usage", WarningValue: 50, CriticalValue: 95),
                    new MetricThreshold("disk.usage", WarningValue: 85, CriticalValue: 95),
                }));

        // Register platform-specific services
        if (OperatingSystem.IsWindows())
        {
            AddWindowsServices(services);
        }
        else if (OperatingSystem.IsLinux())
        {
            AddLinuxServices(services);
        }
        else if (OperatingSystem.IsMacOS())
        {
            AddMacOsServices(services);
        }
        else
        {
            throw new PlatformNotSupportedException("This operating system is not supported.");
        }

        return services;
    }

    /// <summary>
    /// Registers Windows-specific monitoring services using LibreHardwareMonitor
    /// for detailed hardware and sensor information.
    /// </summary>
    private static void AddWindowsServices(IServiceCollection services)
    {
        services.AddSingleton<IHardwareRefreshService>(_ => 
            SystemMonitor.Infrastructure.Monitoring.Windows.LibreHardwareMonitorHost.Instance);
        services.AddSingleton<ICpuMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsCpuMonitorService>();
        services.AddSingleton<IMemoryMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsMemoryMonitorService>();
        services.AddSingleton<IDiskMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsDiskMonitorService>();
        services.AddSingleton<INetworkMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsNetworkMonitorService>();
        services.AddSingleton<IMotherboardMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsMotherboardMonitorService>();
        services.AddSingleton<IGpuMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsGpuMonitorService>();
        services.AddSingleton<IMetricsSnapshotProvider, MetricsSnapshotProvider>();
        services.AddSingleton<IMetricHistoryStore>(_ => new MetricHistoryStore(TimeSpan.FromSeconds(60)));
        services.AddSingleton<IHardwareTreeProvider>(sp =>
            new SystemMonitor.Infrastructure.Monitoring.Windows.WindowsHardwareTreeProvider(
                SystemMonitor.Infrastructure.Monitoring.Windows.LibreHardwareMonitorHost.Instance.Computer));
    }

    /// <summary>
    /// Registers Linux-specific monitoring services.
    /// Uses /proc, /sys, and system utilities for hardware information.
    /// Some sensors may be unavailable without elevated privileges.
    /// </summary>
    private static void AddLinuxServices(IServiceCollection services)
    {
        services.AddSingleton<IHardwareRefreshService>(_ => 
            SystemMonitor.Infrastructure.Monitoring.Linux.LinuxHardwareHost.Instance);
        services.AddSingleton<ICpuMonitorService, SystemMonitor.Infrastructure.Monitoring.Linux.LinuxCpuMonitorService>();
        services.AddSingleton<IMemoryMonitorService, SystemMonitor.Infrastructure.Monitoring.Linux.LinuxMemoryMonitorService>();
        services.AddSingleton<IDiskMonitorService, SystemMonitor.Infrastructure.Monitoring.Linux.LinuxDiskMonitorService>();
        services.AddSingleton<INetworkMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsNetworkMonitorService>();
        services.AddSingleton<IMotherboardMonitorService, SystemMonitor.Infrastructure.Monitoring.Linux.LinuxMotherboardMonitorService>();
        services.AddSingleton<IGpuMonitorService, SystemMonitor.Infrastructure.Monitoring.Linux.LinuxGpuMonitorService>();
        services.AddSingleton<IMetricsSnapshotProvider, MetricsSnapshotProvider>();
        services.AddSingleton<IMetricHistoryStore>(_ => new MetricHistoryStore(TimeSpan.FromSeconds(60)));
        services.AddSingleton<IHardwareTreeProvider, SystemMonitor.Infrastructure.Monitoring.Linux.LinuxHardwareTreeProvider>();
    }

    /// <summary>
    /// Registers macOS-specific monitoring services.
    /// Uses sysctl, system_profiler, IOKit, and other native macOS APIs.
    /// Some sensors may be unavailable without elevated privileges.
    /// </summary>
    private static void AddMacOsServices(IServiceCollection services)
    {
        services.AddSingleton<IHardwareRefreshService>(_ => 
            SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsHardwareHost.Instance);
        services.AddSingleton<ICpuMonitorService, SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsCpuMonitorService>();
        services.AddSingleton<IMemoryMonitorService, SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsMemoryMonitorService>();
        services.AddSingleton<IDiskMonitorService, SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsDiskMonitorService>();
        services.AddSingleton<INetworkMonitorService, SystemMonitor.Infrastructure.Monitoring.Windows.WindowsNetworkMonitorService>();
        services.AddSingleton<IMotherboardMonitorService, SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsMotherboardMonitorService>();
        services.AddSingleton<IGpuMonitorService, SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsGpuMonitorService>();
        services.AddSingleton<IMetricsSnapshotProvider, MetricsSnapshotProvider>();
        services.AddSingleton<IMetricHistoryStore>(_ => new MetricHistoryStore(TimeSpan.FromSeconds(60)));
        services.AddSingleton<IHardwareTreeProvider, SystemMonitor.Infrastructure.Monitoring.MacOS.MacOsHardwareTreeProvider>();
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
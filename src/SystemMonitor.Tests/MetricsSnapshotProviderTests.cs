using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Domain.Models;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;
using Xunit;

namespace SystemMonitor.Tests;

public sealed class MetricsSnapshotProviderTests
{
    [Fact]
    public void ServiceFailure_DoesNotInvalidateHealthyCategories()
    {
        var provider = new MetricsSnapshotProvider(
            new ThrowingCpu(),
            new HealthyMemory(),
            new HealthyDisk(),
            new HealthyNetwork(),
            new EmptyMotherboard(),
            new EmptyGpu(),
            new HealthyOperatingSystem(),
            new MetricHistoryStore());

        var snapshot = provider.GetSnapshot();

        Assert.False(Find(snapshot, "cpu.usage").IsAvailable);
        Assert.Equal("N/A", Find(snapshot, "cpu.usage").DisplayValue);
        Assert.True(Find(snapshot, "memory.usage").IsAvailable);
        Assert.True(Find(snapshot, "disk.total").IsAvailable);
        Assert.True(Find(snapshot, "network.download").IsAvailable);
    }

    [Fact]
    public void HardwareRefresh_IsThrottledAcrossFastSnapshots()
    {
        var refreshService = new CountingHardwareRefresh();
        var provider = new MetricsSnapshotProvider(
            new HealthyCpu(),
            new HealthyMemory(),
            new HealthyDisk(),
            new HealthyNetwork(),
            new EmptyMotherboard(),
            new EmptyGpu(),
            new HealthyOperatingSystem(),
            new MetricHistoryStore(),
            refreshService);

        _ = provider.GetSnapshot();
        _ = provider.GetSnapshot();

        Assert.Equal(1, refreshService.CallCount);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsProcessAccessCheck_AllowsAccessibleProcess()
    {
        using var current = Process.GetCurrentProcess();

        Assert.True(DotNetOsMonitorService.IsProcessAccessible(current));
    }

    private static MetricReading Find(IReadOnlyList<MetricReading> readings, string id) =>
        Assert.Single(readings, reading => reading.Id == id);

    private sealed class HealthyCpu : ICpuMonitorService
    {
        public CpuInfo GetCurrentUsage() => new() { ModelName = "Test CPU", CoreCount = 8, ThreadCount = 16, UsagePercent = 15 };
    }

    private sealed class CountingHardwareRefresh : IHardwareRefreshService
    {
        public int CallCount { get; private set; }

        public void RefreshAll() => CallCount++;
    }

    private sealed class ThrowingCpu : ICpuMonitorService
    {
        public CpuInfo GetCurrentUsage() => throw new InvalidOperationException("test CPU failure");
    }

    private sealed class HealthyMemory : IMemoryMonitorService
    {
        public MemoryInfo GetCurrentUsage() => new() { TotalMB = 1024, AvailableMB = 512, UsedMB = 512, UsagePercent = 50 };
    }

    private sealed class HealthyDisk : IDiskMonitorService
    {
        public DiskInfo GetCurrentUsage() => new() { DriveName = "test", TotalGB = 10, FreeGB = 5, UsedGB = 5 };
    }

    private sealed class HealthyNetwork : INetworkMonitorService
    {
        public NetworkInfo GetCurrentUsage() => new() { DownloadKBPerSec = 10, UploadKBPerSec = 5 };
    }

    private sealed class EmptyMotherboard : IMotherboardMonitorService
    {
        public MotherboardInfo? GetCurrentInfo() => null;
    }

    private sealed class EmptyGpu : IGpuMonitorService
    {
        public IReadOnlyList<GpuInfo> GetCurrentUsage() => Array.Empty<GpuInfo>();
    }

    private sealed class HealthyOperatingSystem : IOsMonitorService
    {
        public OperatingSystemInfo? LastInfo => null;

        public OperatingSystemInfo GetCurrentInfo() => new()
        {
            OsName = "Test OS",
            OsVersion = "1",
            Uptime = TimeSpan.FromMinutes(1)
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Application.Common;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Domain.Models;
using SystemMonitor.Infrastructure.Persistence;
using Xunit;

namespace SystemMonitor.Tests;

public sealed class PerformanceRegressionTests
{
    [Fact]
    public void SnapshotProvider_ReusesHardwareTemperaturesBetweenRefreshes()
    {
        var hardware = new CountingHardwareRefresh();
        var provider = new MetricsSnapshotProvider(
            new HealthyCpu(),
            new HealthyMemory(),
            new HealthyDisk(),
            new HealthyNetwork(),
            new EmptyMotherboard(),
            new EmptyGpu(),
            new HealthyOperatingSystem(),
            new MetricHistoryStore(),
            hardware);

        var first = provider.GetSnapshot();
        var second = provider.GetSnapshot();

        // Two ticks inside the 2 s hardware window must not scan the sensors
        // twice — that was the per-tick cost this regression guards.
        Assert.Equal(1, hardware.RefreshCount);
        Assert.Equal(1, hardware.TemperatureReadCount);
        Assert.Equal(Find(first, "temp.cpu.test-sensor").Value, Find(second, "temp.cpu.test-sensor").Value);
    }

    [Fact]
    public void MetricHistoryStore_ConcurrentRecordAndReadIsSafe()
    {
        var store = new MetricHistoryStore(TimeSpan.FromMinutes(1));
        var snapshot = new[] { new MetricReading { Id = "metric.concurrent", IsAvailable = true, Value = 42 } };

        var workers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 1_000; i++)
                {
                    store.Record(snapshot);
                    store.GetHistory("metric.concurrent");
                    store.GetCommittedRange("metric.concurrent");
                }
            }))
            .ToArray();

        Task.WaitAll(workers);
        Assert.True(store.GetHistory("metric.concurrent").Count > 0);
    }

    [Fact]
    public void CoalescingDispatcher_DoesNotApplyAfterDispose()
    {
        var applied = 0;
        var dispatcher = new CoalescingDispatcher<int>(
            _ => Interlocked.Increment(ref applied),
            quietWindow: TimeSpan.FromSeconds(5));

        dispatcher.Post(1);
        dispatcher.Dispose();
        Thread.Sleep(150);

        Assert.Equal(0, Volatile.Read(ref applied));
    }

    [Fact]
    public async Task HistoryPersistence_PersistsNumericReadingsOnly()
    {
        using var service = new SqliteMetricHistoryPersistenceService();
        var suffix = Guid.NewGuid().ToString("N");
        var numericId = $"test.numeric.{suffix}";
        var textId = $"test.text.{suffix}";

        service.Record(new[]
        {
            new MetricReading { Id = numericId, IsAvailable = true, Kind = MetricKind.Percentage, Value = 12.5 },
            new MetricReading { Id = textId, IsAvailable = true, Kind = MetricKind.Text, TextValue = "hello" }
        });

        var since = DateTime.UtcNow.AddMinutes(-1);
        IReadOnlyList<MetricHistoryPoint> numeric = Array.Empty<MetricHistoryPoint>();

        for (var attempt = 0; attempt < 40 && numeric.Count == 0; attempt++)
        {
            await Task.Delay(100);
            numeric = await service.GetHistoryAsync(numericId, since);
        }

        Assert.Single(numeric);
        Assert.Equal(12.5, numeric[0].Value, 3);
        Assert.Empty(await service.GetHistoryAsync(textId, since));
    }

    private static MetricReading Find(IReadOnlyList<MetricReading> readings, string id) =>
        Assert.Single(readings, reading => reading.Id == id);

    private sealed class CountingHardwareRefresh : IHardwareRefreshService
    {
        public int RefreshCount { get; private set; }
        public int TemperatureReadCount { get; private set; }

        public void RefreshAll() => RefreshCount++;

        public IReadOnlyList<TemperatureReading> GetTemperatureReadings()
        {
            TemperatureReadCount++;
            return new[]
            {
                new TemperatureReading
                {
                    Category = "CPU",
                    HardwareName = "Test CPU",
                    SensorId = "test-sensor",
                    SensorLabel = "Test CPU: Package",
                    IsAvailable = true,
                    TemperatureCelsius = 55
                }
            };
        }
    }

    private sealed class HealthyCpu : ICpuMonitorService
    {
        public CpuInfo GetCurrentUsage() => new() { ModelName = "Test CPU", CoreCount = 8, ThreadCount = 16, UsagePercent = 15 };
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

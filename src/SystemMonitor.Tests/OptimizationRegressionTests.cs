using System;
using System.Collections.Generic;
using System.Linq;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Domain.Models;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;
using Xunit;

namespace SystemMonitor.Tests;

/// <summary>
/// Regression coverage for the hot-path allocations and correctness fixes in
/// this pass: smoothing math, cached temperature ids, history pruning with the
/// reused active-id buffer, process value equality, and system-wide OS totals.
/// </summary>
public sealed class OptimizationRegressionTests
{
    [Fact]
    public void SnapshotProvider_SmoothingAveragesTheLastFourSamples()
    {
        var cpu = new RampingCpu(10, 20, 30, 40, 50);
        var provider = CreateProvider(cpu);

        var values = new List<double>();
        for (var i = 0; i < 5; i++)
            values.Add(Find(provider.GetSnapshot(), "cpu.usage").Value);

        Assert.Equal(new[] { 10d, 15d, 20d, 25d, 35d }, values);
    }

    [Fact]
    public void SnapshotProvider_TemperatureMetricIdsStayStableAcrossTicks()
    {
        var provider = CreateProvider(new RampingCpu(10), sensorId: "probe/cpu");

        var first = provider.GetSnapshot()
            .Where(r => r.Kind == MetricKind.Temperature)
            .Select(r => r.Id)
            .ToArray();
        var second = provider.GetSnapshot()
            .Where(r => r.Kind == MetricKind.Temperature)
            .Select(r => r.Id)
            .ToArray();

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.Equal(first.Length, first.Distinct(StringComparer.Ordinal).Count());
        // Component fallback sensors are keyed by their label (the shared
        // hardware-scan path keys by sensor id), so only the shape is asserted.
        Assert.StartsWith("temp.cpu.", first[0], StringComparison.Ordinal);
    }

    [Fact]
    public void MetricHistoryStore_PrunesMetricsThatDisappearFromTheSnapshot()
    {
        var store = new MetricHistoryStore(TimeSpan.FromMinutes(1));

        store.Record(new[] { Available("metric.a", 10) });
        store.Record(new[] { Available("metric.a", 20) });
        store.Record(new[] { Available("metric.b", 5) });

        Assert.Empty(store.GetHistory("metric.a"));
        Assert.Equal((0d, 1d), store.GetCommittedRange("metric.a"));
        Assert.Single(store.GetHistory("metric.b"));
    }

    [Fact]
    public void MetricHistoryStore_KeepsTheLiveTipWhenNoPointHasGraduated()
    {
        var store = new MetricHistoryStore(TimeSpan.FromMinutes(1));
        store.Record(new[] { Available("metric.tip", 42) });

        var history = store.GetHistory("metric.tip");
        Assert.Single(history);
        Assert.Null(history[0].NormalizedValue);
        Assert.Equal((41d, 43d), store.GetCommittedRange("metric.tip"));
    }

    [Fact]
    public void ProcessInfo_ValuesCompareEqualForIdenticalSamples()
    {
        var first = new ProcessInfo { ProcessId = 42, Name = "probe", CpuPercent = 3.5, WorkingSetMB = 128, ThreadCount = 7 };
        var second = new ProcessInfo { ProcessId = 42, Name = "probe", CpuPercent = 3.5, WorkingSetMB = 128, ThreadCount = 7 };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());

        second.CpuPercent = 4.5;
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ThresholdMonitor_StillFiresOncePerCrossing()
    {
        var log = new RecordingEventLog();
        var monitor = new ThresholdMonitorService(log, new[]
        {
            new MetricThreshold("cpu.usage", WarningValue: 80, CriticalValue: 90)
        });

        var hot = new[] { Available("cpu.usage", 95) };
        monitor.Check(hot);
        monitor.Check(hot);
        monitor.Check(new[] { Available("cpu.usage", 50) });
        monitor.Check(hot);

        Assert.Equal(2, log.Events.Count);
    }

    [Fact]
    public void WindowsOsService_ReportsSystemWideThreadAndHandleTotals()
    {
        if (!OperatingSystem.IsWindows())
            return; // hardware/platform dependent by design

        var info = new DotNetOsMonitorService().GetCurrentInfo();

        Assert.True(info.ProcessCount > 1);
        Assert.True(info.ThreadCount >= info.ProcessCount);
        Assert.True(info.HandleCount is > 0);
    }

    private static MetricReading Available(string id, double value) => new()
    {
        Id = id,
        Kind = MetricKind.Percentage,
        IsAvailable = true,
        Value = value
    };

    private static MetricReading Find(IReadOnlyList<MetricReading> readings, string id) =>
        Assert.Single(readings, reading => reading.Id == id);

    private static MetricsSnapshotProvider CreateProvider(ICpuMonitorService cpu, string sensorId = "probe/cpu") =>
        new(
            cpu,
            new HealthyMemory(),
            new HealthyDisk(),
            new HealthyNetwork(),
            new EmptyMotherboard(),
            new EmptyGpu(),
            new HealthyOperatingSystem(),
            new MetricHistoryStore());

    private sealed class RampingCpu : ICpuMonitorService
    {
        private readonly double[] _usage;
        private int _index;

        public RampingCpu(params double[] usage) => _usage = usage;

        public CpuInfo GetCurrentUsage()
        {
            var value = _usage[Math.Min(_index, _usage.Length - 1)];
            _index++;
            return new CpuInfo
            {
                ModelName = "Probe CPU",
                CoreCount = 8,
                ThreadCount = 16,
                UsagePercent = value,
                Temperatures = new List<TemperatureReading>
                {
                    new()
                    {
                        Category = "CPU",
                        HardwareName = "Probe CPU",
                        SensorId = "probe/cpu",
                        SensorLabel = "Probe CPU: Package",
                        IsAvailable = true,
                        TemperatureCelsius = 55
                    }
                }
            };
        }
    }

    private sealed class RecordingEventLog : IEventLogService
    {
        public List<(string Type, string Message)> Events { get; } = new();

        public void LogEvent(string type, string message, string? metadata = null) =>
            Events.Add((type, message));

        public System.Threading.Tasks.Task<IReadOnlyList<EventLogEntry>> GetEventsAsync(
            string? type = null, DateTime? since = null, int limit = 200) =>
            System.Threading.Tasks.Task.FromResult<IReadOnlyList<EventLogEntry>>(Array.Empty<EventLogEntry>());

        public System.Threading.Tasks.Task DeleteAllEventsAsync() => System.Threading.Tasks.Task.CompletedTask;
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

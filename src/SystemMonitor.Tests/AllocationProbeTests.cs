using System;
using System.Collections.Generic;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace SystemMonitor.Tests;

/// <summary>
/// SQL/DB-free allocation probe for the per-tick snapshot path. It is a
/// budget guard first and an informational measurement second: the polling
/// thread runs this pipeline at the configured tick rate for the entire
/// lifetime of the process, so a regression here is a sustained cost.
/// </summary>
public sealed class AllocationProbeTests
{
    private readonly ITestOutputHelper _output;

    public AllocationProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void SnapshotProvider_AllocationsPerTick_StayWithinBudget()
    {
        var provider = new MetricsSnapshotProvider(
            new ProbeCpu(),
            new ProbeMemory(),
            new ProbeDisk(),
            new ProbeNetwork(),
            new ProbeMotherboard(),
            new ProbeGpu(),
            new ProbeOs(),
            new MetricHistoryStore());

        // Warm up: metric ids, GPU label cache and smoothing windows are
        // populated on the first tick, and that one-time cost is not what
        // this budget is about.
        for (var i = 0; i < 3; i++)
            provider.GetSnapshot();

        const int ticks = 200;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ticks; i++)
            provider.GetSnapshot();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        _output.WriteLine($"SnapshotProvider allocated {allocated} bytes over {ticks} ticks " +
            $"({allocated / (double)ticks:F0} B/tick).");

        // Budget: the provider necessarily allocates one MetricReading per
        // emitted metric (~70 today). Keep the guard loose enough to allow
        // that, tight enough to catch per-tick LINQ/HashSet/dictionary churn.
        Assert.True(allocated / ticks <= 30_000,
            $"Snapshot path allocated {allocated / (double)ticks:F0} B/tick, budget is 30000 B/tick.");
    }

    private sealed class ProbeCpu : ICpuMonitorService
    {
        public CpuInfo GetCurrentUsage() => new()
        {
            ModelName = "Probe CPU",
            CoreCount = 8,
            ThreadCount = 16,
            UsagePercent = 21,
            ClockMhz = 3600,
            Temperatures = new List<TemperatureReading>
            {
                new() { Category = "CPU", HardwareName = "Probe CPU", SensorId = "probe/cpu", SensorLabel = "Probe CPU: Package", IsAvailable = true, TemperatureCelsius = 54 }
            }
        };
    }

    private sealed class ProbeMemory : IMemoryMonitorService
    {
        public MemoryInfo GetCurrentUsage() => new()
        {
            TotalMB = 16 * 1024,
            AvailableMB = 8 * 1024,
            UsedMB = 8 * 1024,
            UsagePercent = 50,
            PartNumber = "PROBE-16G",
            Type = "DDR4",
            SpeedMhz = 3200,
            ModuleConfig = "2 x 8 GB",
            Manufacturer = "Probe",
            Temperatures = new List<TemperatureReading>()
        };
    }

    private sealed class ProbeDisk : IDiskMonitorService
    {
        public DiskInfo GetCurrentUsage() => new()
        {
            DriveName = "probe",
            TotalGB = 512,
            FreeGB = 256,
            UsedGB = 256,
            UsagePercent = 50,
            ReadMBPerSec = 3,
            WriteMBPerSec = 4,
            Model = "Probe SSD",
            DiskType = "SSD",
            BusType = "NVMe",
            FileSystem = "NTFS",
            Temperatures = new List<TemperatureReading>()
        };
    }

    private sealed class ProbeNetwork : INetworkMonitorService
    {
        public NetworkInfo GetCurrentUsage() => new() { DownloadKBPerSec = 120, UploadKBPerSec = 30 };
    }

    private sealed class ProbeMotherboard : IMotherboardMonitorService
    {
        public MotherboardInfo? GetCurrentInfo() => new(
            Model: "Probe Board", Chipset: "Probe", TemperatureCelsius: 35)
        {
            Temperatures = new List<TemperatureReading>
            {
                new() { Category = "MOTHERBOARD", HardwareName = "Probe Board", SensorId = "probe/board", SensorLabel = "Probe Board: System", IsAvailable = true, TemperatureCelsius = 35 }
            }
        };
    }

    private sealed class ProbeGpu : IGpuMonitorService
    {
        public IReadOnlyList<GpuInfo> GetCurrentUsage() => new[]
        {
            new GpuInfo
            {
                IsAvailable = true,
                DeviceId = "0x0000_0x0001",
                Index = 0,
                Name = "Probe GPU",
                Vendor = "Probe",
                IsIntegrated = false,
                DedicatedMemoryTotalMb = 8192,
                DedicatedMemoryUsedMb = 2048,
                UsagePercent = 33,
                Temperatures = new List<TemperatureReading>
                {
                    new() { Category = "GPU", HardwareName = "Probe GPU", SensorId = "probe/gpu", SensorLabel = "Probe GPU: GPU Core", IsAvailable = true, TemperatureCelsius = 62 }
                }
            }
        };
    }

    private sealed class ProbeOs : IOsMonitorService
    {
        private static readonly List<ProcessInfo> Processes = new()
        {
            new ProcessInfo { ProcessId = 1, Name = "probe", CpuPercent = 5, WorkingSetMB = 100, ThreadCount = 10 }
        };

        public OperatingSystemInfo? LastInfo => null;

        public OperatingSystemInfo GetCurrentInfo() => new()
        {
            OsName = "Probe OS",
            OsVersion = "1.0",
            Uptime = TimeSpan.FromHours(3),
            ProcessCount = 180,
            ThreadCount = 2400,
            HandleCount = 90000,
            TopProcesses = Processes,
            TopProcessesByCpu = Processes,
            TopProcessesByMemory = Processes
        };
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management; // requires the "System.Management" NuGet package
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsGpuMonitorService : IGpuMonitorService
{
    // Static hardware facts don't change while the app is running,
    // so we read them once here instead of on every GetCurrentUsage() call —
    // same idea as WindowsCpuMonitorService warming up its counter once.
    private readonly string _name = "Unknown";
    private readonly string _vendor = "Unknown";
    private readonly string _driverVersion = "Unknown";
    private readonly double _totalMemoryMb;
    private readonly bool _isIntegrated;

    // Counters must stay alive between calls — rate-based counters like
    // "Utilization Percentage" always return 0 on their first-ever read,
    // so a counter that's recreated every call would ALWAYS report 0%
    // regardless of real GPU load. Keying by instance name also lets us
    // handle instances appearing/disappearing as apps start/stop using the GPU.
    private readonly Dictionary<string, PerformanceCounter> _engineCounters = new();

    public WindowsGpuMonitorService()
    {
        // On hybrid systems (e.g. laptop with Intel iGPU + NVIDIA dGPU),
        // WMI lists both. We look at every entry and prefer the dedicated
        // one as "the" GPU this service reports on, instead of blindly
        // taking whichever WMI happens to return first.
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
        ManagementObject? chosen = null;
        bool chosenIsIntegrated = true;
        foreach (ManagementObject obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "Unknown";
            var memoryMb = Convert.ToDouble(obj["AdapterRAM"] ?? 0) / (1024 * 1024);
            var integrated = LooksIntegrated(name, memoryMb);
            // First GPU found becomes the default; a later dedicated GPU
            // always overrides an earlier integrated one.
            if (chosen == null || (chosenIsIntegrated && !integrated))
            {
                chosen = obj;
                chosenIsIntegrated = integrated;
            }
        }
        if (chosen != null)
        {
            _name = chosen["Name"]?.ToString() ?? "Unknown";
            _vendor = chosen["AdapterCompatibility"]?.ToString() ?? "Unknown";
            _driverVersion = chosen["DriverVersion"]?.ToString() ?? "Unknown";
            _totalMemoryMb = Convert.ToDouble(chosen["AdapterRAM"] ?? 0) / (1024 * 1024);
            _isIntegrated = chosenIsIntegrated;
        }
        // Prime whatever GPU Engine counter instances exist right now, same
        // idea as WindowsCpuMonitorService warming up its counter in the
        // constructor. This fixes the "first read is always 0%" issue for
        // the common case. Caveat: GPU Engine instances can appear later
        // too (e.g. when a new app starts using the GPU) — those brand-new
        // instances will still report 0% on their own first read, since
        // there's no way to prime a counter that doesn't exist yet.
        GetGpuUsagePercent();
    }

    // Heuristic only — Windows doesn't expose an "integrated vs dedicated"
    // flag directly without a vendor SDK. We combine two signals:
    //  1. Name pattern: integrated GPUs have recognizable names.
    //  2. Reported VRAM: integrated GPUs typically report little/no
    //     dedicated memory since they borrow system RAM instead.
    private static bool LooksIntegrated(string name, double memoryMb)
    {
        var lowerName = name.ToLowerInvariant();
        bool nameLooksIntegrated =
            lowerName.Contains("intel") ||
            lowerName.Contains("uhd graphics") ||
            lowerName.Contains("iris") ||
            lowerName.Contains("radeon(tm) graphics"); // common AMD APU naming
        bool memoryLooksIntegrated = memoryMb <= 512;
        return nameLooksIntegrated || memoryLooksIntegrated;
    }

    public GpuInfo GetCurrentUsage()
    {
        return new GpuInfo
        {
            Name = _name,
            Vendor = _vendor,
            DriverVersion = _driverVersion,
            DedicatedMemoryTotalMb = _totalMemoryMb,
            DedicatedMemoryUsedMb = GetGpuMemoryUsedMb(),
            IsIntegrated = _isIntegrated,
            UsagePercent = GetGpuUsagePercent(),
            PowerUsage = ReadPackagePowerWatts(),
            Timestamp = DateTime.UtcNow
        };
    }

    // Watts come from the shared LibreHardwareMonitor host (NVAPI-backed on
    // NVIDIA) — a different channel than the performance counters above.
    // Ask NVIDIA first, then AMD, then Intel; first non-null answer wins.
    private static double? ReadPackagePowerWatts()
    {
        var host = LibreHardwareMonitorHost.Instance;
        return host.GetPackagePowerWatts(HardwareType.GpuNvidia)
            ?? host.GetPackagePowerWatts(HardwareType.GpuAmd)
            ?? host.GetPackagePowerWatts(HardwareType.GpuIntel);
    }

    // "GPU Process Memory" is a separate counter category from "GPU Engine",
    // with one instance per process using the GPU. "Dedicated Usage" is a
    // raw instantaneous counter (bytes currently in use) — unlike
    // Utilization Percentage, it does NOT need priming/warm-up, since it's
    // not rate-based. We sum across every process to get total VRAM in use.
    // Caveat: on a hybrid system this sums usage from ALL GPUs' processes
    // combined, since there's no vendor SDK to filter by which physical
    // GPU a given process's memory belongs to.
    private double GetGpuMemoryUsedMb()
    {
        var category = new PerformanceCounterCategory("GPU Process Memory");
        double totalBytes = 0;
        foreach (var instance in category.GetInstanceNames())
        {
            foreach (var counter in category.GetCounters(instance))
            {
                if (counter.CounterName == "Dedicated Usage")
                {
                    totalBytes += counter.RawValue;
                }
            }
        }
        return totalBytes / (1024 * 1024);
    }

    // Unlike CPU's single "_Total" counter, GPU Engine exposes one counter
    // instance per active engine (3D, copy, video decode, etc.), and those
    // instances can appear/disappear as apps use the GPU. We cache one
    // PerformanceCounter per instance so repeated calls read the SAME
    // object over time (required for a rate-based counter to report
    // anything other than 0) rather than creating a fresh one every call.
    // We sum just the 3D engine instances to approximate Task Manager's GPU %.
    private double GetGpuUsagePercent()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var currentInstances = category.GetInstanceNames()
            .Where(i => i.Contains("engtype_3D"))
            .ToHashSet();
        // Drop counters for instances that no longer exist (e.g. the app
        // using that engine slot was closed).
        foreach (var staleKey in _engineCounters.Keys.Except(currentInstances).ToList())
        {
            _engineCounters[staleKey].Dispose();
            _engineCounters.Remove(staleKey);
        }
        double total = 0;
        foreach (var instance in currentInstances)
        {
            if (!_engineCounters.TryGetValue(instance, out var counter))
            {
                // Brand-new instance: create and prime it. Its first real
                // value will come on the NEXT call to this method, once
                // this cached counter has been read a second time.
                counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                counter.NextValue();
                _engineCounters[instance] = counter;
                continue;
            }
            total += counter.NextValue();
        }
        return Math.Round(total, 2);
    }
}
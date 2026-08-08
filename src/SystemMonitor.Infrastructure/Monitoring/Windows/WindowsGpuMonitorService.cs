using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management; // requires the "System.Management" NuGet package
using Vortice.DXGI;       // requires the "Vortice.DXGI" NuGet package
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

/// <summary>
/// Windows implementation of IGpuMonitorService.
///
/// Deliberately avoids vendor SDKs (NVML, ADL, etc.) as instructed.
/// Instead it uses three built-in/OS-level Windows sources:
///   1. WMI (Win32_VideoController) — static info: name, vendor,
///      driver version, total memory.
///   2. Performance Counters ("GPU Engine" / "GPU Process Memory") —
///      live usage % and VRAM used. Available since Windows 10, works
///      for any GPU brand.
///   3. DXGI (via the Vortice.DXGI wrapper) — a core Windows graphics
///      API, not a vendor SDK, used only to resolve which LUID belongs
///      to our chosen GPU, so we can filter the counters above to that
///      SPECIFIC adapter instead of summing every GPU on the system.
///
/// Trade-off: without a vendor SDK we can't get exact temperature,
/// fan speed, or power draw on Windows — those fields stay null here.
/// </summary>
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

    // The specific fragment of a counter instance name that identifies
    // OUR chosen GPU (e.g. "luid_0x00000000_0x0000abcd"). Null if DXGI
    // couldn't resolve a match — in that case we fall back to summing
    // every GPU on the system rather than reporting nothing.
    private readonly string? _luidFilter;

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

        var (luidFilter, accurateMemoryMb) = ResolveGpuDetailsFromDxgi(_name);
        _luidFilter = luidFilter;

        // DXGI's DedicatedVideoMemory is a 64-bit value with no size cap,
        // unlike WMI's AdapterRAM (32-bit, caps out around 4095MB). Prefer
        // it when DXGI successfully matched our GPU.
        if (accurateMemoryMb.HasValue)
        {
            _totalMemoryMb = accurateMemoryMb.Value;
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

    // DXGI is Microsoft's own graphics API (ships with Windows), not a
    // vendor SDK. We use it for two things once at startup:
    //   1. Matching our chosen GPU's name to its LUID, a unique adapter
    //      identifier, which lets us filter Performance Counter instances
    //      to just this GPU (see MatchesChosenGpu).
    //   2. Reading DedicatedVideoMemory — a 64-bit VRAM total with no
    //      size cap, unlike WMI's 32-bit AdapterRAM field which maxes
    //      out around 4095MB on cards with more VRAM than that.
    private static (string? luidFilter, double? memoryMb) ResolveGpuDetailsFromDxgi(string gpuName)
    {
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                using (adapter)
                {
                    var desc = adapter.Description1;
                    if (string.Equals(desc.Description, gpuName, StringComparison.OrdinalIgnoreCase))
                    {
                        var luid = desc.Luid;
                        var luidFilter = $"0x{(uint)luid.HighPart:x8}_0x{luid.LowPart:x8}";
                        var memoryMb = desc.DedicatedVideoMemory / (1024.0 * 1024.0);
                        return (luidFilter, memoryMb);
                    }
                }
            }
        }
        catch
        {
            // If DXGI enumeration fails for any reason, fall back to
            // summing all GPUs for usage/memory and keeping WMI's
            // (possibly capped) total, rather than crashing entirely.
        }

        return (null, null);
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
            Timestamp = DateTime.UtcNow
        };
    }

    // Unlike CPU's single "_Total" counter, GPU Engine exposes one counter
    // instance per active engine (3D, copy, video decode, etc.), and those
    // instances can appear/disappear as apps use the GPU. We cache one
    // PerformanceCounter per instance so repeated calls read the SAME
    // object over time (required for a rate-based counter to report
    // anything other than 0) rather than creating a fresh one every call.
    // Filtered to _luidFilter so only OUR chosen GPU's 3D engine counts —
    // without this, a hybrid system's integrated GPU usage would get
    // added into the total alongside the dedicated card.
    private double GetGpuUsagePercent()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var currentInstances = category.GetInstanceNames()
            .Where(i => i.Contains("engtype_3D") && MatchesChosenGpu(i))
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

    // "GPU Process Memory" is a separate counter category from "GPU Engine",
    // with one instance per process using the GPU. "Dedicated Usage" is a
    // raw instantaneous counter (bytes currently in use) — it does NOT need
    // priming/warm-up, since it's not rate-based. Filtered to _luidFilter
    // for the same reason as usage above: isolate to just our chosen GPU.
    private double GetGpuMemoryUsedMb()
    {
        var category = new PerformanceCounterCategory("GPU Process Memory");
        double totalBytes = 0;

        foreach (var instance in category.GetInstanceNames())
        {
            if (!MatchesChosenGpu(instance)) continue;

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

    // If DXGI couldn't resolve a LUID for our chosen GPU (rare — falls
    // back gracefully), we accept every instance rather than reporting
    // nothing, same spirit as the original "sum everything" behavior.
    private bool MatchesChosenGpu(string instanceName) =>
        _luidFilter == null || instanceName.Contains(_luidFilter, StringComparison.OrdinalIgnoreCase);
}
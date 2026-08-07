using System;
using System.Diagnostics;
using System.Management; // requires the "System.Management" NuGet package
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

    public WindowsGpuMonitorService()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
        foreach (ManagementObject obj in searcher.Get())
        {
            _name = obj["Name"]?.ToString() ?? "Unknown";
            _vendor = obj["AdapterCompatibility"]?.ToString() ?? "Unknown";
            _driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";
            _totalMemoryMb = Convert.ToDouble(obj["AdapterRAM"] ?? 0) / (1024 * 1024);
            break; // primary GPU only
        }
    }

    public GpuInfo GetCurrentUsage()
    {
        return new GpuInfo
        {
            Name = _name,
            Vendor = _vendor,
            DriverVersion = _driverVersion,
            DedicatedMemoryTotalMb = _totalMemoryMb,
            UsagePercent = GetGpuUsagePercent(),
            Timestamp = DateTime.UtcNow
        };
    }

    // Unlike CPU's single "_Total" counter, GPU Engine exposes one counter
    // instance per active engine (3D, copy, video decode, etc.), and those
    // instances can appear/disappear as apps use the GPU — so we can't warm
    // these up once in the constructor the way the CPU counter is warmed up.
    // We sum just the 3D engine instances to approximate Task Manager's GPU %.
    private double GetGpuUsagePercent()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        double total = 0;

        foreach (var instance in category.GetInstanceNames())
        {
            if (!instance.Contains("engtype_3D")) continue;

            foreach (var counter in category.GetCounters(instance))
            {
                if (counter.CounterName == "Utilization Percentage")
                {
                    total += counter.NextValue();
                }
            }
        }

        return Math.Round(total, 2);
    }
}

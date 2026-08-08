using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using Vortice.DXGI;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsGpuMonitorService : IGpuMonitorService
{
    private sealed record GpuDevice(
        int Index,
        string Name,
        string Vendor,
        string DriverVersion,
        bool IsIntegrated,
        string? LuidFilter,
        IDXGIAdapter3? Adapter3,      
        double WmiFallbackMemoryMb);  

    private readonly List<GpuDevice> _devices = new();
    private readonly Dictionary<string, PerformanceCounter> _engineCounters = new();

    public WindowsGpuMonitorService()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

        int index = 0;
        foreach (ManagementObject obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "Unknown";
            var vendor = obj["AdapterCompatibility"]?.ToString() ?? "Unknown";
            var driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";
            var wmiMemoryMb = Convert.ToDouble(obj["AdapterRAM"] ?? 0) / (1024 * 1024);
            var integrated = LooksIntegrated(name, wmiMemoryMb);

            var (luidFilter, adapter3) = ResolveGpuAdapterFromDxgi(name);

            _devices.Add(new GpuDevice(
                Index: index,
                Name: name,
                Vendor: vendor,
                DriverVersion: driverVersion,
                IsIntegrated: integrated,
                LuidFilter: luidFilter,
                Adapter3: adapter3,
                WmiFallbackMemoryMb: wmiMemoryMb));

            index++;
        }

        foreach (var device in _devices)
        {
            GetGpuUsagePercent(device.LuidFilter);
        }
    }

    private static (string? luidFilter, IDXGIAdapter3? adapter3) ResolveGpuAdapterFromDxgi(string gpuName)
    {
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                var desc = adapter.Description1;
                if (string.Equals(desc.Description, gpuName, StringComparison.OrdinalIgnoreCase))
                {
                    var luid = desc.Luid;
                    var luidFilter = $"0x{(uint)luid.HighPart:x8}_0x{luid.LowPart:x8}";

                    var adapter3 = adapter.QueryInterface<IDXGIAdapter3>();
                    return (luidFilter, adapter3);
                }

                adapter.Dispose();
            }
        }
        catch
        {
            // fall back to unfiltered/WMI rather than crashing
        }

        return (null, null);
    }

    private static bool LooksIntegrated(string name, double memoryMb)
    {
        var lowerName = name.ToLowerInvariant();
        bool nameLooksIntegrated =
            lowerName.Contains("intel") ||
            lowerName.Contains("uhd graphics") ||
            lowerName.Contains("iris") ||
            lowerName.Contains("radeon(tm) graphics");

        bool memoryLooksIntegrated = memoryMb <= 512;

        return nameLooksIntegrated || memoryLooksIntegrated;
    }

    public IReadOnlyList<GpuInfo> GetCurrentUsage()
    {
        return _devices.Select(device => new GpuInfo
        {
            IsAvailable = true,
            Index = device.Index,
            Name = device.Name,
            Vendor = device.Vendor,
            DriverVersion = device.DriverVersion,
            DedicatedMemoryTotalMb = GetLiveMemoryBudgetMb(device),
            DedicatedMemoryUsedMb = GetGpuMemoryUsedMb(device),
            IsIntegrated = device.IsIntegrated,
            UsagePercent = GetGpuUsagePercent(device.LuidFilter),
            Timestamp = DateTime.UtcNow
        }).ToList();
    }

    private static double GetLiveMemoryBudgetMb(GpuDevice device)
    {
        if (device.Adapter3 is not null)
        {
            try
            {
                var info = device.Adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
                return Math.Round(info.Budget / (1024.0 * 1024.0), 2);
            }
            catch
            {
                // fall through to WMI fallback below
            }
        }

        return device.WmiFallbackMemoryMb;
    }

    private static double GetGpuMemoryUsedMb(GpuDevice device)
    {
        if (device.Adapter3 is not null)
        {
            try
            {
                // Queries current hardware VRAM usage directly from DXGI
                var info = device.Adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
                return Math.Round(info.CurrentUsage / (1024.0 * 1024.0), 2);
            }
            catch
            {
                // DXGI query fallback
            }
        }

        return 0;
    }

    private double GetGpuUsagePercent(string? luidFilter)
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var allInstances = category.GetInstanceNames()
            .Where(i => i.Contains("engtype_3D"))
            .ToHashSet();

        foreach (var staleKey in _engineCounters.Keys.Except(allInstances).ToList())
        {
            _engineCounters[staleKey].Dispose();
            _engineCounters.Remove(staleKey);
        }

        double total = 0;
        foreach (var instance in allInstances.Where(i => MatchesGpu(i, luidFilter)))
        {
            if (!_engineCounters.TryGetValue(instance, out var counter))
            {
                counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                counter.NextValue();
                _engineCounters[instance] = counter;
                continue;
            }

            total += counter.NextValue();
        }

        return Math.Round(total, 2);
    }

    private static bool MatchesGpu(string instanceName, string? luidFilter) =>
        luidFilter == null || instanceName.Contains(luidFilter, StringComparison.OrdinalIgnoreCase);
}
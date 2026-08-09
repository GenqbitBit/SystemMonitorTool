using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using LibreHardwareMonitor.Hardware;
using Vortice.DXGI;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsGpuMonitorService : IGpuMonitorService, IDisposable
{
    private sealed record GpuDevice(
        int Index,
        string Name,
        string Vendor,
        string DriverVersion,
        bool IsIntegrated,
        string? LuidFilter,          // used only for GPU Engine (usage %) perf-counter matching
        IHardware? LibreHardware);   // used only for VRAM total/used sensors

    private readonly List<GpuDevice> _devices = new();
    private readonly Dictionary<string, PerformanceCounter> _engineCounters = new();
    private readonly Computer _computer;

    public WindowsGpuMonitorService()
    {
        _computer = new Computer { IsGpuEnabled = true };
        _computer.Open();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

        int index = 0;
        foreach (ManagementObject obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "Unknown";
            var vendor = obj["AdapterCompatibility"]?.ToString() ?? "Unknown";
            var driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";
            var wmiMemoryMb = Convert.ToDouble(obj["AdapterRAM"] ?? 0) / (1024 * 1024);
            var integrated = LooksIntegrated(name, wmiMemoryMb);

            var luidFilter = ResolveGpuLuidFromDxgi(name);
            var libreHardware = ResolveGpuHardwareFromLibre(name);

            _devices.Add(new GpuDevice(
                Index: index,
                Name: name,
                Vendor: vendor,
                DriverVersion: driverVersion,
                IsIntegrated: integrated,
                LuidFilter: luidFilter,
                LibreHardware: libreHardware));

            index++;
        }

        foreach (var device in _devices)
        {
            GetGpuUsagePercent(device.LuidFilter);
        }
    }

    // Unchanged: still needed for GPU Engine (usage %) perf-counter matching, out of scope for this pass.
    private static string? ResolveGpuLuidFromDxgi(string gpuName)
    {
        var seenDxgiNames = new List<string>();

        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                var desc = adapter.Description1;
                var dxgiName = (desc.Description ?? string.Empty).Trim();
                seenDxgiNames.Add(dxgiName);

                if (string.Equals(dxgiName, gpuName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    var luid = desc.Luid;
                    adapter.Dispose();
                    return $"0x{(uint)luid.HighPart:x8}_0x{luid.LowPart:x8}";
                }

                adapter.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GPU] DXGI adapter enumeration failed while resolving '{gpuName}': {ex}");
            return null;
        }

        Debug.WriteLine(
            $"[GPU] No DXGI adapter matched WMI name '{gpuName}'. " +
            $"DXGI adapters seen: {string.Join(", ", seenDxgiNames.Select(n => $"'{n}'"))}");
        return null;
    }

    // New: resolves the LibreHardwareMonitor GPU hardware handle used for VRAM total/used.
    private IHardware? ResolveGpuHardwareFromLibre(string gpuName)
    {
        var candidates = _computer.Hardware
            .Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            .ToList();

        var match = candidates.FirstOrDefault(h =>
            string.Equals(h.Name?.Trim(), gpuName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            Debug.WriteLine(
                $"[GPU] No LibreHardwareMonitor device matched WMI name '{gpuName}'. " +
                $"Devices seen: {string.Join(", ", candidates.Select(h => $"'{h.Name}'"))}");
        }

        return match;
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
        return _devices.Select(device =>
        {
            var (totalMb, usedMb) = GetGpuMemoryMb(device);

            return new GpuInfo
            {
                IsAvailable = true,
                Index = device.Index,
                Name = device.Name,
                Vendor = device.Vendor,
                DriverVersion = device.DriverVersion,
                DedicatedMemoryTotalMb = totalMb,
                DedicatedMemoryUsedMb = usedMb,
                IsIntegrated = device.IsIntegrated,
                UsagePercent = GetGpuUsagePercent(device.LuidFilter),
                Timestamp = DateTime.UtcNow
            };
        }).ToList();
    }

    // Replaces old DXGI static bytes + WMI AdapterRAM fallback. Single Update() call
    // per read so Total/Used come from the same sensor snapshot.
    private static (double totalMb, double usedMb) GetGpuMemoryMb(GpuDevice device)
    {
        if (device.LibreHardware is null)
        {
            // No matching LibreHardwareMonitor device — memory reporting unavailable
            // for this GPU. 0 here means "unknown", same caveat as before.
            return (0, 0);
        }

        device.LibreHardware.Update();

        double totalMb = 0;
        double usedMb = 0;

        foreach (var sensor in device.LibreHardware.Sensors)
        {
            if (sensor.SensorType != SensorType.SmallData || sensor.Value is not float value)
            {
                continue;
            }

            if (sensor.Name == "GPU Memory Total")
            {
                totalMb = Math.Round(value, 2);
            }
            else if (sensor.Name == "GPU Memory Used")
            {
                usedMb = Math.Round(value, 2);
            }
        }

        return (totalMb, usedMb);
    }

    // Unchanged: GPU Engine usage % still comes from PerformanceCounter, out of scope for this pass.
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

    // New: exposes the same WMI-ordered Index/Name/IsIntegrated used by GetCurrentUsage(),
    // so WindowsTemperatureMonitorService (or anything else) can match its own GPU hardware
    // handles to these same indices instead of enumerating GPUs independently.
    public IReadOnlyList<GpuDeviceIdentity> GetDeviceIdentities() =>
        _devices.Select(d => new GpuDeviceIdentity(d.Index, d.Name, d.IsIntegrated)).ToList();

    // New: Computer is a disposable resource introduced by LibreHardwareMonitorLib.
    public void Dispose()
    {
        foreach (var counter in _engineCounters.Values)
        {
            counter.Dispose();
        }

        _computer.Close();
    }
}
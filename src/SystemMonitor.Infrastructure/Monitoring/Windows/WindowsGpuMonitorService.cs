using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using Vortice.DXGI;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

[SupportedOSPlatform("windows")]
public class WindowsGpuMonitorService : IGpuMonitorService, IDisposable
{
    private sealed record GpuDevice(
        int Index,
        string Name,
        string Vendor,
        string DriverVersion,
        bool IsIntegrated,
        string DeviceId,             // stable identity — LUID string, or index-based fallback
        string? LuidFilter,          // used only for GPU Engine (usage %) perf-counter matching
        IHardware? LibreHardware);   // used for VRAM total/used sensors AND temperature sensors

    private readonly List<GpuDevice> _devices = new();
    private readonly Dictionary<string, PerformanceCounter> _engineCounters = new();
    private readonly Computer _computer;
    private readonly Dictionary<string, (double Sum, int Count)> _temperatureAveraging = new();
    private readonly Dictionary<string, (double Value, DateTime RefreshedUtc)> _usageCache = new();
    private readonly object _usageCacheGate = new();

    // Enumerating the "GPU Engine" performance category is extremely
    // expensive (hundreds of instances, registry-backed). Keep the category
    // and the instance list instead of rebuilding both per device per tick.
    private PerformanceCounterCategory? _engineCategory;
    private string[] _engineInstanceNames = Array.Empty<string>();
    private readonly HashSet<string> _engineInstanceSet = new(StringComparer.Ordinal);
    private readonly List<string> _staleEngineCounters = new();
    private DateTime _engineInstancesUtc = DateTime.MinValue;
    private static readonly TimeSpan UsageRefreshInterval = TimeSpan.FromMilliseconds(1500);

    public WindowsGpuMonitorService()
    {
        _computer = LibreHardwareMonitorHost.Instance.Computer;

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

        int index = 0;
        foreach (ManagementObject obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "Unknown";
            var vendor = obj["AdapterCompatibility"]?.ToString() ?? "Unknown";
            var driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";

            var luidFilter = ResolveGpuLuidFromDxgi(name);
            var libreHardware = ResolveGpuHardwareFromLibre(name);

            // Win32_VideoController.AdapterRAM is a 32-bit field that overflows
            // for any GPU with ~4GB+ VRAM (e.g. a 6GB RTX 4050 reports back
            // garbage or near-zero), which was silently misclassifying dedicated
            // GPUs as integrated. LibreHardwareMonitor reads VRAM size straight
            // from the driver and doesn't have this problem — prefer it, and
            // only fall back to WMI's AdapterRAM if no Libre hardware resolved
            // for this device at all.
            var libreMemoryMb = GetLibreGpuMemoryTotalMb(libreHardware);
            var memoryMb = libreMemoryMb > 0
                ? libreMemoryMb
                : Convert.ToDouble(obj["AdapterRAM"] ?? 0) / (1024 * 1024);

            var integrated = LooksIntegrated(name, memoryMb);

            Debug.WriteLine(
            $"[GPU] '{name}': libreMemoryMb={libreMemoryMb}, wmiMemoryMb=" +
            $"{Convert.ToDouble(obj["AdapterRAM"] ?? 0) / (1024 * 1024)}, " +
            $"resolvedMemoryMb={memoryMb}, integrated={integrated}");

            // DeviceId is the identity every downstream layer keys on. LUID when
            // DXGI resolved it; otherwise a logged fallback so a real resolution
            // gap stays visible instead of silently degrading to enumeration order.
            var deviceId = luidFilter;
            if (deviceId is null)
            {
                deviceId = $"gpu-fallback-{index}";
                Debug.WriteLine(
                    $"[GPU] No stable LUID identity for '{name}' — falling back to " +
                    $"'{deviceId}'. This device's identity is not guaranteed stable " +
                    $"across refreshes if enumeration order changes.");
            }

            _devices.Add(new GpuDevice(
                Index: index,
                Name: name,
                Vendor: vendor,
                DriverVersion: driverVersion,
                IsIntegrated: integrated,
                DeviceId: deviceId,
                LuidFilter: luidFilter,
                LibreHardware: libreHardware));

            index++;
        }

        foreach (var device in _devices)
        {
            GetGpuUsagePercent(device.LuidFilter);
        }
    }

    private static string? ResolveGpuLuidFromDxgi(string gpuName)
    {
        var seenDxgiNames = new List<string>();
        var normalizedTarget = NormalizeGpuName(gpuName);

        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                var desc = adapter.Description1;
                var dxgiName = (desc.Description ?? string.Empty).Trim();
                seenDxgiNames.Add(dxgiName);

                if (string.Equals(NormalizeGpuName(dxgiName), normalizedTarget, StringComparison.OrdinalIgnoreCase)
                    || (normalizedTarget.Length > 0 && NormalizeGpuName(dxgiName).Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    || (normalizedTarget.Length > 0 && normalizedTarget.Contains(NormalizeGpuName(dxgiName), StringComparison.OrdinalIgnoreCase)))
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

    private IHardware? ResolveGpuHardwareFromLibre(string gpuName)
    {
        var candidates = _computer.Hardware
            .Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            .ToList();

        var normalizedTarget = NormalizeGpuName(gpuName);
        var match = candidates.FirstOrDefault(h =>
            string.Equals(NormalizeGpuName(h.Name), normalizedTarget, StringComparison.OrdinalIgnoreCase)
            || (normalizedTarget.Length > 0 && NormalizeGpuName(h.Name).Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase))
            || (normalizedTarget.Length > 0 && normalizedTarget.Contains(NormalizeGpuName(h.Name), StringComparison.OrdinalIgnoreCase)));

        if (match is null)
        {
            Debug.WriteLine(
                $"[GPU] No LibreHardwareMonitor device matched WMI name '{gpuName}'. " +
                $"Devices seen: {string.Join(", ", candidates.Select(h => $"'{h.Name}'"))}");
        }

        return match;
    }

    private static string NormalizeGpuName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim();
        normalized = new string(normalized
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray());

        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
    }

    // Reads total VRAM (MB) straight from LibreHardwareMonitor's "GPU Memory
    // Total" sensor — used instead of WMI's AdapterRAM for integrated/dedicated
    // classification, since AdapterRAM is a 32-bit field that overflows on
    // GPUs with ~4GB+ VRAM. Returns 0 if no Libre hardware resolved or the
    // sensor isn't present, signaling the caller to fall back to WMI.
    private static double GetLibreGpuMemoryTotalMb(IHardware? hardware)
    {
        if (hardware is null)
            return 0;

        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.SmallData
                    && sensor.Name == "GPU Memory Total"
                    && sensor.Value is float value)
                {
                    return Math.Round(value, 2);
                }
            }
        }

        return 0;
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
                DeviceId = device.DeviceId,
                Index = device.Index,
                Name = device.Name,
                Vendor = device.Vendor,
                DriverVersion = device.DriverVersion,
                DedicatedMemoryTotalMb = totalMb,
                DedicatedMemoryUsedMb = usedMb,
                IsIntegrated = device.IsIntegrated,
                UsagePercent = GetGpuUsagePercent(device.LuidFilter),
                Temperatures = GetGpuTemperatures(device),
                Timestamp = DateTime.UtcNow
            };
        }).ToList();
    }

    private static (double totalMb, double usedMb) GetGpuMemoryMb(GpuDevice device)
    {
        if (device.LibreHardware is null)
        {
            return (0, 0);
        }

        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
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
    }

    // Reads this device's slice of the shared temperature cache. The old
    // version walked (and re-read) every GPU sensor on every tick.
    private List<TemperatureReading> GetGpuTemperatures(GpuDevice device)
    {
        var cached = LibreHardwareMonitorHost.Instance.GetTemperatureReadings("GPU");
        if (cached.Count == 0)
            return new List<TemperatureReading>();

        var hardwareName = device.LibreHardware?.Name;
        var readings = new List<TemperatureReading>(cached.Count);

        foreach (var source in cached)
        {
            if (hardwareName is not null
                && !string.Equals(source.HardwareName, hardwareName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsLimitSensor(source.SensorLabel))
                continue;

            readings.Add(WithAverage(source));
        }

        // Preserve the previous "GPU Core is primary" display rule. The
        // cache marks every sensor primary, so choose one per device here.
        if (readings.Count > 0)
        {
            var primaryIndex = -1;
            for (var i = 0; i < readings.Count; i++)
            {
                if (readings[i].SensorLabel.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
                {
                    primaryIndex = i;
                    break;
                }
            }

            if (primaryIndex < 0)
            {
                for (var i = 0; i < readings.Count; i++)
                {
                    if (readings[i].SensorLabel.Contains("core", StringComparison.OrdinalIgnoreCase))
                    {
                        primaryIndex = i;
                        break;
                    }
                }
            }

            if (primaryIndex < 0)
                primaryIndex = 0;

            for (var i = 0; i < readings.Count; i++)
                readings[i].IsPrimary = i == primaryIndex;
        }

        return readings;
    }

    private static bool IsLimitSensor(string label) =>
        label.Contains("Warning", StringComparison.OrdinalIgnoreCase)
        || label.Contains("Critical", StringComparison.OrdinalIgnoreCase)
        || label.Contains("Limit", StringComparison.OrdinalIgnoreCase)
        || label.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase);

    private TemperatureReading WithAverage(TemperatureReading source)
    {
        double average = 0;

        if (source.IsAvailable)
        {
            var key = source.SensorId;
            if (_temperatureAveraging.TryGetValue(key, out var existing))
            {
                var newSum = existing.Sum + source.TemperatureCelsius;
                var newCount = existing.Count + 1;
                _temperatureAveraging[key] = (newSum, newCount);
                average = newSum / newCount;
            }
            else
            {
                _temperatureAveraging[key] = (source.TemperatureCelsius, 1);
                average = source.TemperatureCelsius;
            }
        }

        return new TemperatureReading
        {
            Category = source.Category,
            HardwareName = source.HardwareName,
            SensorId = source.SensorId,
            SensorLabel = source.SensorLabel,
            IsAvailable = source.IsAvailable,
            TemperatureCelsius = source.TemperatureCelsius,
            MinCelsius = source.MinCelsius,
            MaxCelsius = source.MaxCelsius,
            AverageCelsius = average,
            IsPrimary = source.IsPrimary
        };
    }

    private double GetGpuUsagePercent(string? luidFilter)
    {
        if (string.IsNullOrWhiteSpace(luidFilter))
            return 0;

        var now = DateTime.UtcNow;
        lock (_usageCacheGate)
        {
            if (_usageCache.TryGetValue(luidFilter, out var cached)
                && now - cached.RefreshedUtc < UsageRefreshInterval)
            {
                return cached.Value;
            }
        }

        try
        {
            // Create the category once, and enumerate instance names at most
            // once per refresh interval — shared by every GPU device. Doing
            // this per device per tick was the single most expensive monitor
            // operation in the app.
            _engineCategory ??= new PerformanceCounterCategory("GPU Engine");

            if (now - _engineInstancesUtc >= UsageRefreshInterval)
            {
                _engineInstanceNames = _engineCategory.GetInstanceNames();
                _engineInstancesUtc = now;

                _engineInstanceSet.Clear();
                foreach (var instanceName in _engineInstanceNames)
                    _engineInstanceSet.Add(instanceName);

                _staleEngineCounters.Clear();
                foreach (var key in _engineCounters.Keys)
                {
                    if (!_engineInstanceSet.Contains(key))
                        _staleEngineCounters.Add(key);
                }

                foreach (var key in _staleEngineCounters)
                {
                    _engineCounters[key].Dispose();
                    _engineCounters.Remove(key);
                }
            }
        }
        catch
        {
            lock (_usageCacheGate)
                _usageCache[luidFilter] = (0, now);
            return 0;
        }

        double total = 0;
        foreach (var instance in _engineInstanceNames)
        {
            if (!instance.Contains("engtype_3D", StringComparison.Ordinal))
                continue;
            if (!instance.Contains(luidFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_engineCounters.TryGetValue(instance, out var counter))
            {
                try
                {
                    counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                    counter.NextValue();
                    _engineCounters[instance] = counter;
                }
                catch
                {
                    // Engine disappeared between enumeration and first read.
                }
                continue;
            }

            try
            {
                total += counter.NextValue();
            }
            catch
            {
                counter.Dispose();
                _engineCounters.Remove(instance);
            }
        }

        var rounded = Math.Round(total, 2);
        lock (_usageCacheGate)
            _usageCache[luidFilter] = (rounded, now);
        return rounded;
    }

    public void Dispose()
    {
        lock (_usageCacheGate)
        {
            foreach (var counter in _engineCounters.Values)
                counter.Dispose();

            _engineCounters.Clear();
            _usageCache.Clear();
            _engineCategory = null;
            _engineInstanceNames = Array.Empty<string>();
        }
    }
}
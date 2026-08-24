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
    private readonly Dictionary<ISensor, (double Sum, int Count)> _temperatureAveraging = new();
    private readonly Dictionary<string, double> _usageCache = new();
    private readonly object _usageCacheGate = new();
    private DateTime _lastUsageRefreshUtc = DateTime.MinValue;
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

    private List<TemperatureReading> GetGpuTemperatures(GpuDevice device)
    {
        if (device.LibreHardware is null)
        {
            return new List<TemperatureReading>();
        }

        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            var temperatureSensors = device.LibreHardware.Sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .Where(s => !s.Name.Contains("Warning", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (temperatureSensors.Count == 0)
            {
                return new List<TemperatureReading>();
            }

            var primarySensorName = DeterminePrimaryGpuSensorName(temperatureSensors, device.Name);

            var readings = temperatureSensors
                .Select(sensor => BuildTemperatureReading(
                    sensor,
                    isPrimary: string.Equals(sensor.Name?.Trim(), primarySensorName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            DisambiguateDuplicateLabels(readings);
            return readings;
        }
    }

    private TemperatureReading BuildTemperatureReading(ISensor sensor, bool isPrimary)
    {
        var isRealReading = sensor.Value.HasValue && sensor.Value.Value != 0;
        double average = 0, min = 0, max = 0;

        if (isRealReading)
        {
            var currentValue = sensor.Value!.Value;
            if (_temperatureAveraging.TryGetValue(sensor, out var existing))
            {
                var newSum = existing.Sum + currentValue;
                var newCount = existing.Count + 1;
                _temperatureAveraging[sensor] = (newSum, newCount);
                average = newSum / newCount;
            }
            else
            {
                _temperatureAveraging[sensor] = (currentValue, 1);
                average = currentValue;
            }
            min = sensor.Min ?? currentValue;
            max = sensor.Max ?? currentValue;
        }

        return new TemperatureReading
        {
            SensorLabel = sensor.Name ?? "Unknown",
            IsAvailable = isRealReading,
            TemperatureCelsius = isRealReading ? sensor.Value!.Value : 0,
            MinCelsius = min,
            MaxCelsius = max,
            AverageCelsius = average,
            IsPrimary = isPrimary
        };
    }

    private static string? DeterminePrimaryGpuSensorName(List<ISensor> temperatureSensors, string hardwareName)
    {
        var exactMatch = temperatureSensors.FirstOrDefault(s =>
            string.Equals(s.Name?.Trim(), "GPU Core", StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return exactMatch.Name;
        }

        var containsMatch = temperatureSensors.FirstOrDefault(s =>
            s.Name?.Contains("core", StringComparison.OrdinalIgnoreCase) == true);
        if (containsMatch is not null)
        {
            Debug.WriteLine(
                $"[Temp] GPU '{hardwareName}' has no sensor named exactly 'GPU Core' — " +
                $"using '{containsMatch.Name}' as the primary reading instead. " +
                $"Sensors seen: {string.Join(", ", temperatureSensors.Select(s => $"'{s.Name}'"))}");
            return containsMatch.Name;
        }

        var fallback = temperatureSensors[0];
        Debug.WriteLine(
            $"[Temp] GPU '{hardwareName}' has no sensor with 'core' in its name — " +
            $"falling back to '{fallback.Name}' (first sensor) as the primary reading. " +
            $"Sensors seen: {string.Join(", ", temperatureSensors.Select(s => $"'{s.Name}'"))}");
        return fallback.Name;
    }

    private static void DisambiguateDuplicateLabels(List<TemperatureReading> readings)
    {
        var groups = readings.GroupBy(r => r.SensorLabel);
        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var index = 1;
            foreach (var reading in group)
            {
                reading.SensorLabel = $"{reading.SensorLabel} #{index}";
                index++;
            }
        }
    }

    private double GetGpuUsagePercent(string? luidFilter)
    {
        if (string.IsNullOrWhiteSpace(luidFilter))
            return 0;

        var now = DateTime.UtcNow;
        lock (_usageCacheGate)
        {
            if (_usageCache.TryGetValue(luidFilter, out var cached)
                && now - _lastUsageRefreshUtc < UsageRefreshInterval)
            {
                return cached;
            }
        }

        PerformanceCounterCategory category;
        try
        {
            category = new PerformanceCounterCategory("GPU Engine");
        }
        catch
        {
            lock (_usageCacheGate)
            {
                _usageCache[luidFilter] = 0;
                _lastUsageRefreshUtc = now;
            }
            return 0;
        }

        string[] instanceNames;
        try
        {
            instanceNames = category.GetInstanceNames();
        }
        catch
        {
            lock (_usageCacheGate)
            {
                _usageCache[luidFilter] = 0;
                _lastUsageRefreshUtc = now;
            }
            return 0;
        }

        var allInstances = instanceNames
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
                try
                {
                    counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                    counter.NextValue();
                    _engineCounters[instance] = counter;
                    continue;
                }
                catch
                {
                    continue;
                }
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
        {
            _usageCache[luidFilter] = rounded;
            _lastUsageRefreshUtc = now;
        }
        return rounded;
    }

    private static bool MatchesGpu(string instanceName, string? luidFilter) =>
    luidFilter != null && instanceName.Contains(luidFilter, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        foreach (var counter in _engineCounters.Values)
        {
            counter.Dispose();
        }
    }
}
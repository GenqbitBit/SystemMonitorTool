using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsTemperatureMonitorService : ITemperatureMonitorService, IDisposable
{
    private readonly Computer _computer;
    private readonly Dictionary<ISensor, (double Sum, int Count)> _averageTracking = new();

    // Device identities from WindowsGpuMonitorService's WMI-ordered list, so a
    // GPU's temperature readings carry the SAME Index/IsIntegrated used for
    // its usage/VRAM readings elsewhere in the app.
    private readonly IReadOnlyList<GpuDeviceIdentity> _gpuIdentities;

    public WindowsTemperatureMonitorService(IGpuMonitorService gpuMonitor)
    {
        _gpuIdentities = gpuMonitor.GetDeviceIdentities();

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true
        };

        _computer.Open();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
                subHardware.Update();
        }
    }

    public List<TemperatureReading> GetCurrentUsage()
    {
        var readings = new List<TemperatureReading>();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            CollectTemperatureSensors(hardware, readings);

            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
                CollectTemperatureSensors(subHardware, readings);
            }
        }

        DisambiguateDuplicateLabels(readings);

        return readings;
    }

    private void CollectTemperatureSensors(IHardware hardware, List<TemperatureReading> readings)
    {
        var category = hardware.HardwareType switch
        {
            HardwareType.Cpu => "CPU",
            HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
            HardwareType.Storage => "Disk",
            HardwareType.Motherboard => "Motherboard",
            _ => null
        };

        if (category is null) return;

        // Only meaningful for GPU rows — resolves which WMI-ordered device this
        // LibreHardwareMonitor hardware handle corresponds to, same matching
        // pattern WindowsGpuMonitorService uses for its own VRAM sensors.
        GpuDeviceIdentity? gpuIdentity = null;
        if (category == "GPU")
        {
            gpuIdentity = _gpuIdentities.FirstOrDefault(g =>
                string.Equals(g.Name?.Trim(), hardware.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (gpuIdentity is null)
            {
                Debug.WriteLine(
                    $"[Temp] No GPU device identity matched LibreHardwareMonitor name '{hardware.Name}'. " +
                    $"Known devices: {string.Join(", ", _gpuIdentities.Select(g => $"'{g.Name}'"))}");
            }
        }

        var temperatureSensors = hardware.Sensors
            .Where(s => s.SensorType == SensorType.Temperature)
            .Where(s => !s.Name.Contains("Warning", StringComparison.OrdinalIgnoreCase)
                     && !s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                     && !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                     && !s.Name.Contains("Distance to TjMax", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Only meaningful for GPU rows — which sensor on this device counts as
        // the primary/"core" reading. Falls back gracefully (with a log) if the
        // usual "GPU Core" name isn't present, rather than leaving every sensor
        // on the device non-primary with no indication why.
        string? primarySensorName = null;
        if (category == "GPU" && temperatureSensors.Count > 0)
        {
            primarySensorName = DeterminePrimaryGpuSensorName(temperatureSensors, hardware.Name);
        }

        foreach (var sensor in temperatureSensors)
        {
            // A reading of exactly 0°C is never real for these components — see
            // investigation notes: confirmed not permissions/version/AV related,
            // appears to be OEM firmware restricting SMU telemetry on some laptops.
            var isRealReading = sensor.Value.HasValue && sensor.Value.Value != 0;

            double average = 0;
            double min = 0;
            double max = 0;

            if (isRealReading)
            {
                var currentValue = sensor.Value!.Value;

                if (_averageTracking.TryGetValue(sensor, out var existing))
                {
                    var newSum = existing.Sum + currentValue;
                    var newCount = existing.Count + 1;
                    _averageTracking[sensor] = (newSum, newCount);
                    average = newSum / newCount;
                }
                else
                {
                    _averageTracking[sensor] = (currentValue, 1);
                    average = currentValue;
                }

                // Min/Max are tracked natively by the library since Computer.Open() —
                // no need to compute these ourselves
                min = sensor.Min ?? currentValue;
                max = sensor.Max ?? currentValue;
            }

            // The device's main die temp is the "core" reading; everything else
            // reported for the same GPU (Hot Spot, Memory Junction, VRAM, Fan,
            // etc.) is a sub-reading under it. See DeterminePrimaryGpuSensorName
            // for how the core sensor is chosen, including its fallback.
            var isPrimary = category != "GPU" || string.Equals(sensor.Name?.Trim(), primarySensorName, StringComparison.OrdinalIgnoreCase);

            readings.Add(new TemperatureReading
            {
                Category = category,
                SensorLabel = sensor.Name ?? "Unknown",
                IsAvailable = isRealReading,
                TemperatureCelsius = isRealReading ? sensor.Value!.Value : 0,
                MinCelsius = min,
                MaxCelsius = max,
                AverageCelsius = average,
                GpuIndex = gpuIdentity?.Index,
                GpuIsIntegrated = gpuIdentity?.IsIntegrated,
                IsPrimary = isPrimary
            });
        }
    }

    // Picks which GPU sensor counts as the device's primary/"core" reading.
    // Tier 1: exact "GPU Core" — LibreHardwareMonitorLib's usual name across
    //         Nvidia/AMD/Intel.
    // Tier 2: any sensor whose name contains "core" (case-insensitive) — covers
    //         naming drift on less common Intel/driver combos.
    // Tier 3: the first sensor in enumeration order — so a device NEVER ends up
    //         with zero primary sensors; a fallback log line explains why.
    // Every tier below 1 is logged so a real naming gap is visible rather than
    // silently absorbed.
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
        // Grouped by GpuIndex too, so two GPUs each having e.g. a "GPU Core"
        // sensor don't get suffixed against each other — only true duplicates
        // within the SAME device (or non-GPU categories) get "#1"/"#2".
        var groups = readings.GroupBy(r => (r.Category, r.GpuIndex, r.SensorLabel));

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

    public void Dispose()
    {
        _computer.Close();
    }
}
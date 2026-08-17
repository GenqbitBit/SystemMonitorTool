using LibreHardwareMonitor.Hardware;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

/// <summary>
/// The single owner of the LibreHardwareMonitor Computer instance.
/// LibreHardwareMonitor loads a kernel driver to read hardware sensors;
/// opening more than one Computer per process risks duplicate driver
/// handles and conflicting reads. This class therefore exists exactly
/// once (static Instance) and every monitor service borrows it.
///
/// Why static instead of DI registration? It guarantees one instance
/// without touching the dependency-injection setup. If the team later
/// prefers constructor injection, convert Instance into a registered
/// singleton and inject it — the rest of the code stays the same.
/// </summary>
public sealed class LibreHardwareMonitorHost
{
    /// <summary>The one and only instance, created on first use.</summary>
    public static LibreHardwareMonitorHost Instance { get; } = new();

    /// <summary>The shared hardware view every service reads from.</summary>
    public Computer Computer { get; }

    private LibreHardwareMonitorHost()
    {
        Computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
        };
        Computer.Open();

        // Warm-up pass so the very first real read already has data behind it.
        foreach (var hardware in Computer.Hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
                subHardware.Update();
        }
    }

    /// <summary>
    /// Package power draw in watts ("CPU Package" / "GPU Package" sensor).
    /// Returns null when the sensor is missing OR reads exactly zero —
    /// zero watts is never physically real, it means the channel failed.
    /// </summary>
    public double? GetPackagePowerWatts(HardwareType type)
    {
        foreach (var hardware in Computer.Hardware)
        {
            if (hardware.HardwareType != type) continue;
            hardware.Update();

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Power &&
                    sensor.Name.Contains("Package"))
                    return PositiveOrNull(sensor.Value);
            }
        }
        return null;
    }

    /// <summary>
    /// Average core clock in MHz ("Cores (Average)" sensor).
    /// Exact name match on purpose: "Cores (Average Effective)" also
    /// CONTAINS "Cores (Average", so a substring test would be ambiguous.
    /// </summary>
    public double? GetCpuClockMhz()
    {
        foreach (var hardware in Computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu) continue;
            hardware.Update();

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Clock &&
                    sensor.Name == "Cores (Average)")
                    return PositiveOrNull(sensor.Value);
            }
        }
        return null;
    }

    private static double? PositiveOrNull(float? value) =>
        value is { } v && v > 0 ? v : null;
}
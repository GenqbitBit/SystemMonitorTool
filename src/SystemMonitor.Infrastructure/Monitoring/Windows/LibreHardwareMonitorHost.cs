using LibreHardwareMonitor.Hardware;
using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;

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
[SupportedOSPlatform("windows")]
public sealed class LibreHardwareMonitorHost : IHardwareRefreshService, IDisposable
{
    /// <summary>The one and only instance, created on first use.</summary>
    public static LibreHardwareMonitorHost Instance { get; } = new();

    /// <summary>
    /// All hardware updates must flow through this single gate so multiple
    /// monitor services do not call Update() concurrently against the same
    /// LibreHardwareMonitor Computer instance.
    /// </summary>
    public object UpdateSyncRoot { get; } = new();

    /// <summary>The shared hardware view every service reads from.</summary>
    public Computer Computer { get; }

    public void RefreshAll()
    {
        lock (UpdateSyncRoot)
        {
            foreach (var hardware in Computer.Hardware)
                UpdateRecursive(hardware);
        }
    }

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
        lock (UpdateSyncRoot)
        {
            foreach (var hardware in Computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                    subHardware.Update();
            }
        }
    }

    /// <summary>
    /// Package power draw in watts ("CPU Package" / "GPU Package" sensor).
    /// Returns null when the sensor is missing OR reads exactly zero —
    /// zero watts is never physically real, it means the channel failed.
    /// </summary>
    public double? GetPackagePowerWatts(HardwareType type)
    {
        lock (UpdateSyncRoot)
        {
            foreach (var hardware in Computer.Hardware)
            {
                if (hardware.HardwareType != type) continue;
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Power &&
                        sensor.Name.Contains("Package"))
                        return PositiveOrNull(sensor.Value);
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Average core clock in MHz ("Cores (Average)" sensor).
    /// Exact name match on purpose: "Cores (Average Effective)" also
    /// CONTAINS "Cores (Average", so a substring test would be ambiguous.
    /// </summary>
    public double? GetCpuClockMhz()
    {
        lock (UpdateSyncRoot)
        {
            foreach (var hardware in Computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu) continue;
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Clock &&
                        sensor.Name == "Cores (Average)")
                        return PositiveOrNull(sensor.Value);
                }
            }
            return null;
        }
    }

    private static double? PositiveOrNull(float? value) =>
        value is { } v && v > 0 ? v : null;

    private static void UpdateRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            UpdateRecursive(subHardware);
    }

    public void Dispose()
    {
        lock (UpdateSyncRoot)
            Computer.Close();
    }
}
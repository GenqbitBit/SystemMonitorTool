using System;

namespace SystemMonitor.Domain.Models;

/// <summary>
/// Represents a single point-in-time snapshot of GPU statistics.
/// This is a pure data model — it has no idea *how* these values
/// were collected. That job belongs to Infrastructure. Keeping this
/// class "dumb" is what lets Domain stay independent of Windows/
/// Linux/macOS specifics, per the project's Clean Architecture rule.
/// </summary>
public class GpuInfo
{
    // False when no GPU could be detected on this machine, or detection
    // failed. When false, every other field should be treated as
    // meaningless default data, not a real reading.
    public bool IsAvailable { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;

    // True for integrated GPUs (e.g. Intel UHD/Iris, AMD APU graphics),
    // false for dedicated cards (e.g. NVIDIA GeForce, AMD Radeon RX).
    // Inferred heuristically — Windows doesn't expose this directly
    // without a vendor SDK, so treat it as a best guess, not a guarantee.
    public bool IsIntegrated { get; set; }

    // Utilization, 0-100
    public double UsagePercent { get; set; }

    // Memory, in MB, so every platform reports the same unit
    public double DedicatedMemoryUsedMb { get; set; }
    public double DedicatedMemoryTotalMb { get; set; }
    public double SharedMemoryUsedMb { get; set; }

    // Nullable because not every OS/driver combo can report these
    // without a vendor SDK we're deliberately not using yet.
    public double? Temperature { get; set; }
    public double? FanSpeed { get; set; }
    public double? PowerUsage { get; set; }
    public double? CoreClockMhz { get; set; }
    public double? MemoryClockMhz { get; set; }

    public string DriverVersion { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

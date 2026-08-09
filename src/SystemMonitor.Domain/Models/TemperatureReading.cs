namespace SystemMonitor.Domain.Models;

/// <summary>
/// A single temperature sensor reading. Owned by whichever hardware model it
/// belongs to (CpuInfo.Temperatures, GpuInfo.Temperatures, DiskInfo.Temperatures) —
/// it no longer carries Category/GpuIndex/GpuIsIntegrated, since that identity
/// is now implicit in which model's list it lives in.
/// </summary>
public class TemperatureReading
{
    public string SensorLabel { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public double TemperatureCelsius { get; set; }
    public double MinCelsius { get; set; }
    public double MaxCelsius { get; set; }
    public double AverageCelsius { get; set; }

    // True for the device's main/core sensor (e.g. "GPU Core", "CPU Package"),
    // false for sub-readings under the same device (Hot Spot, Memory Junction,
    // etc.). Devices that only ever report one sensor (Disk) are always primary.
    public bool IsPrimary { get; set; } = true;
}
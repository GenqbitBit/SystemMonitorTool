namespace SystemMonitor.Domain.Models;

public class TemperatureReading
{
    public string Category { get; set; } = string.Empty;
    public string SensorLabel { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public double TemperatureCelsius { get; set; }
    public double MinCelsius { get; set; }
    public double MaxCelsius { get; set; }
    public double AverageCelsius { get; set; }

    // Only set when Category == "GPU". Matches the same Index/IsIntegrated
    // that WindowsGpuMonitorService uses for gpu.usage.{index}/gpu.memory.{index},
    // via IGpuMonitorService.GetDeviceIdentities() — so "GPU 0" here is the
    // same physical device as "GPU 0" in the usage/VRAM panel.
    public int? GpuIndex { get; set; }
    public bool? GpuIsIntegrated { get; set; }

    // True for the device's main/core sensor (e.g. "GPU Core"), false for
    // sub-readings under that same device (Hot Spot, Memory Junction, etc.).
    // Meaningless outside GPU rows today — defaults true elsewhere so nothing
    // else changes behavior.
    public bool IsPrimary { get; set; } = true;
}
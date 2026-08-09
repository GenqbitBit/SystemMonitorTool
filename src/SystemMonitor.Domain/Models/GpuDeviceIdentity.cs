namespace SystemMonitor.Domain.Models;

/// <summary>
/// Lightweight, no-live-read identity for a GPU device. Exists so services
/// that each enumerate GPUs independently (WindowsGpuMonitorService via WMI,
/// WindowsTemperatureMonitorService via LibreHardwareMonitorLib) can agree on
/// which physical GPU is "Index 0", "Index 1", etc. Not a live reading —
/// consumers match on Name to line this up with their own hardware handle.
/// </summary>
public sealed record GpuDeviceIdentity(int Index, string Name, bool IsIntegrated);
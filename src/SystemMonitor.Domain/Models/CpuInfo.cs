namespace SystemMonitor.Domain.Models;

public class CpuInfo
{
    public double UsagePercent { get; set; }

    // e.g. "AMD Ryzen 5 5600G with Radeon Graphics" — registry, driver-free.
    public string ModelName { get; set; } = string.Empty;

    // Current average clock in MHz from the OS performance counter —
    // driver-free, so it always has a number.
    public double? ClockMhz { get; set; }

    // Physical cores / logical processors (threads) — WMI, driver-free.
    public int CoreCount { get; set; }
    public int ThreadCount { get; set; }

    // Whole-package power draw in watts. The ONE value that physically
    // requires the LibreHardwareMonitor kernel driver on AMD; null when
    // that channel is unavailable, shown as an honest "—".
    public double? PackagePowerWatts { get; set; }
}
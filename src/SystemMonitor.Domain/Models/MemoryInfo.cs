namespace SystemMonitor.Domain.Models;

public class MemoryInfo
{
    public double TotalMB { get; set; }
    public double AvailableMB { get; set; }
    public double UsedMB { get; set; }
    public double UsagePercent { get; set; }

    // Identity facts from WMI (Win32_PhysicalMemory)
    public string PartNumber { get; set; } = string.Empty; // closest thing to a RAM "name"
    public string Type { get; set; } = string.Empty;         // e.g. "DDR4"
    public int SpeedMhz { get; set; }                        // e.g. 3200
    public string ModuleConfig { get; set; } = string.Empty; // e.g. "2 x 8 GB"
    public string Manufacturer { get; set; } = string.Empty; // e.g. "Corsair"
}
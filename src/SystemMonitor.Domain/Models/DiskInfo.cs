using System.Collections.Generic;

namespace SystemMonitor.Domain.Models;

public class DiskInfo
{
    public string DriveName { get; set; } = string.Empty;
    public double TotalGB { get; set; }
    public double FreeGB { get; set; }
    public double UsedGB { get; set; }
    public double UsagePercent { get; set; }
    public double ReadMBPerSec { get; set; }
    public double WriteMBPerSec { get; set; }

    // Identity facts from WMI
    public string Model { get; set; } = string.Empty;      // e.g. "KINGSTON SNV2S500G"
    public string DiskType { get; set; } = string.Empty;   // "SSD" / "HDD"
    public string BusType { get; set; } = string.Empty;    // "NVMe" / "SATA" / ...
    public string FileSystem { get; set; } = string.Empty; // "NTFS"

    // SMART temperature sensor(s) for this drive, via LibreHardwareMonitor.
    // Empty when no matching hardware handle was found (e.g. USB enclosures
    // that don't expose SMART temp, or a name-matching miss).
    public List<TemperatureReading> Temperatures { get; set; } = new();
}
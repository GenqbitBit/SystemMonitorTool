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
}
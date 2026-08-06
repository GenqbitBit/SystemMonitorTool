namespace SystemMonitor.Domain.Models;

public class MemoryInfo
{
    public double TotalMB { get; set; }
    public double AvailableMB { get; set; }
    public double UsedMB { get; set; }
    public double UsagePercent { get; set; }
}
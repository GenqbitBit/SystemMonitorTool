namespace SystemMonitor.Domain.Models;

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string Name { get; set; } = string.Empty;

    // Share of CPU between two samples; the service will compute it
    // from TotalProcessorTime deltas — driver-free.
    public double CpuPercent { get; set; }

    // Physical memory currently charged to this process (WorkingSet64 / 1024 / 1024).
    public double WorkingSetMB { get; set; }

    public int ThreadCount { get; set; }
}
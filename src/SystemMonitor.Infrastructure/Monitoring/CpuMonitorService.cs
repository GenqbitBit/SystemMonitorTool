using System.Diagnostics;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring;

public class CpuMonitorService : ICpuMonitorService
{
    private readonly PerformanceCounter _cpuCounter;

    public CpuMonitorService()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue(); // first call always returns 0 — "warms up" the counter
    }

    public CpuInfo GetCurrentUsage()
    {
        return new CpuInfo
        {
            UsagePercent = _cpuCounter.NextValue()
        };
    }
}
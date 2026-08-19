using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IOsMonitorService
{
    OperatingSystemInfo? LastInfo { get; }

    // One atomic snapshot of the host OS: identity, uptime,
    // process/thread/handle totals, and the current top consumers.
    // Must be cheap and safe to call on every UI tick.
    OperatingSystemInfo GetCurrentInfo();
}
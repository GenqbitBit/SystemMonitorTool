using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IEventLogService
{
    void LogEvent(string type, string message, string? metadata = null);

    Task<IReadOnlyList<EventLogEntry>> GetEventsAsync(
        string? type = null,
        DateTime? since = null,
        int limit = 200);
}
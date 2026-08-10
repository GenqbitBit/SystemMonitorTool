using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

/// <summary>
/// Persists metric readings to disk so history survives an app restart,
/// separate from IMetricHistoryStore's short in-memory rolling window
/// (which stays exactly as-is — it's correct for the live graphs it feeds).
/// This is for actual long-term "Historical Charts", per the README's
/// Database section: SQLite for history, live reads always come from the OS.
/// </summary>
public interface IMetricHistoryPersistenceService
{
    /// <summary>
    /// Queues a snapshot for background persistence. Non-blocking —
    /// safe to call every tick from the UI thread's DispatcherTimer.
    /// </summary>
    void Record(IReadOnlyList<MetricReading> snapshot);

    /// <summary>
    /// Reads persisted history for one metric, from "since" up to now.
    /// </summary>
    Task<IReadOnlyList<MetricHistoryPoint>> GetHistoryAsync(string metricId, DateTime since);
}
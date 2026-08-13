using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IMetricHistoryStore
{
    void Record(IReadOnlyList<MetricReading> snapshot);

    /// <summary>
    /// Frozen history (oldest→newest) plus the current live tip appended
    /// last, if it's still within the window. NormalizedValue is set on
    /// every frozen point and null on the live tip.
    /// </summary>
    IReadOnlyList<MetricHistoryPoint> GetHistory(string metricId);

    /// <summary>
    /// The value range historical points for this metric were normalized
    /// against. Expand-only — grows only when a point graduates outside the
    /// current bounds, never shrinks, and is never touched by the live tip.
    /// </summary>
    (double Min, double Max) GetCommittedRange(string metricId);

    TimeSpan Window { get; }
}
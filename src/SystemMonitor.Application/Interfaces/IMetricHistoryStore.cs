using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IMetricHistoryStore
{
    void Record(IReadOnlyList<MetricReading> snapshot);
    IReadOnlyList<MetricHistoryPoint> GetHistory(string metricId);
    TimeSpan Window { get; }
}
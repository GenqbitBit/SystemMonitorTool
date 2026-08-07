using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IMetricsSnapshotProvider
{
    IReadOnlyList<MetricReading> GetSnapshot();
}
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.UseCases;

public sealed class MetricHistoryStore : IMetricHistoryStore
{
    private readonly TimeSpan _window;
    private readonly Dictionary<string, Queue<MetricHistoryPoint>> _history = new();

    public MetricHistoryStore(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromSeconds(60);
    }

    public TimeSpan Window => _window;

    public void Record(IReadOnlyList<MetricReading> snapshot)
    {
        var now = DateTime.UtcNow;
        foreach (var reading in snapshot)
        {
            if (!reading.IsAvailable) continue;

            if (!_history.TryGetValue(reading.Id, out var queue))
            {
                queue = new Queue<MetricHistoryPoint>();
                _history[reading.Id] = queue;
            }

            queue.Enqueue(new MetricHistoryPoint(now, reading.Value));

            while (queue.Count > 0 && now - queue.Peek().Timestamp > _window)
                queue.Dequeue();
        }
    }

    public IReadOnlyList<MetricHistoryPoint> GetHistory(string metricId) =>
        _history.TryGetValue(metricId, out var queue)
            ? queue.ToArray()
            : Array.Empty<MetricHistoryPoint>();
}
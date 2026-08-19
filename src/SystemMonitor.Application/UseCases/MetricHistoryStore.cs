using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.UseCases;

public sealed class MetricHistoryStore : IMetricHistoryStore
{
    private readonly TimeSpan _window;

    // Frozen, already-graduated points only. Nothing the live tip does ever
    // touches this queue — that's what makes the past immutable.
    private readonly Dictionary<string, Queue<MetricHistoryPoint>> _history = new();

    // The single newest sample per metric. Unfrozen: NormalizedValue stays
    // null and it isn't in _history yet. It graduates (freezes, gets pushed
    // into _history) the instant the NEXT sample for that metric arrives.
    private readonly Dictionary<string, MetricHistoryPoint> _liveTip = new();

    // Expand-only value range per metric. Grows only when a point graduates
    // with a value outside the current bounds — never from the live tip's
    // momentary value, and never shrinks. Widening it later doesn't move
    // anything already frozen, because the frozen NormalizedValue was baked
    // in using the range as it stood at that point's own graduation moment.
    private readonly Dictionary<string, (double Min, double Max)> _committedRange = new();

    // Record() runs on the dedicated polling thread; GetHistory()/
    // GetCommittedRange() run on the UI thread during Render(). All three
    // touch the same Dictionary/Queue fields above with no other
    // synchronization, so every access is serialized through this lock.
    // Without it, a render can enumerate a Queue<T> mid-Enqueue/Dequeue on
    // the other thread — a torn read that returns an internally
    // inconsistent slice of history, which is what produced the
    // appearing/disappearing "cycling" segments.
    private readonly object _lock = new();

    public MetricHistoryStore(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromSeconds(60);
    }

    public TimeSpan Window => _window;

    public void Record(IReadOnlyList<MetricReading> snapshot)
    {
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            foreach (var reading in snapshot)
            {
                if (!reading.IsAvailable) continue;

                GraduateLiveTip(reading.Id, now);

                // The new sample becomes the live tip: unfrozen, not yet in
                // history, doesn't touch the committed range yet.
                _liveTip[reading.Id] = new MetricHistoryPoint(now, reading.Value);
            }
        }
    }

    // Caller must already hold _lock — this is a private helper split out
    // of Record for readability, not a separately-synchronized entry point.
    private void GraduateLiveTip(string metricId, DateTime now)
    {
        if (!_liveTip.TryGetValue(metricId, out var graduating))
            return; // first-ever sample for this metric — nothing to graduate yet

        // The ONE moment the committed range is allowed to change — and only
        // using the point that's actually graduating, never the tip that's
        // about to replace it.
        var range = _committedRange.TryGetValue(metricId, out var existing)
            ? existing
            : (Min: graduating.Value, Max: graduating.Value);

        var min = Math.Min(range.Min, graduating.Value);
        var max = Math.Max(range.Max, graduating.Value);
        _committedRange[metricId] = (min, max);

        // Freeze this point's normalized position against the range as it
        // stands right now. Never recomputed again, no matter how much the
        // committed range grows afterward.
        var frozenRange = max - min;
        var normalized = Math.Abs(frozenRange) < 0.0001
            ? 0.5
            : (graduating.Value - min) / frozenRange;

        if (!_history.TryGetValue(metricId, out var queue))
        {
            queue = new Queue<MetricHistoryPoint>();
            _history[metricId] = queue;
        }

        queue.Enqueue(graduating with { NormalizedValue = normalized });

        while (queue.Count > 0 && now - queue.Peek().Timestamp > _window)
            queue.Dequeue();
    }

    public IReadOnlyList<MetricHistoryPoint> GetHistory(string metricId)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var frozen = new List<MetricHistoryPoint>(
                (_history.TryGetValue(metricId, out var existingQueue) ? existingQueue.Count : 0) + 1);
            if (_history.TryGetValue(metricId, out var queue))
            {
                while (queue.Count > 0 && now - queue.Peek().Timestamp > _window)
                    queue.Dequeue();

                frozen.AddRange(queue);
            }

            if (_liveTip.TryGetValue(metricId, out var tip))
            {
                if (now - tip.Timestamp <= _window)
                    frozen.Add(tip);
                else
                    _liveTip.Remove(metricId);
            }

            if (frozen.Count == 0 && !_liveTip.ContainsKey(metricId))
            {
                _history.Remove(metricId);
                _committedRange.Remove(metricId);
            }

            return frozen;
        }
    }

    public (double Min, double Max) GetCommittedRange(string metricId)
    {
        lock (_lock)
        {
            if (_committedRange.TryGetValue(metricId, out var range))
                return range;

            // No graduated points yet — fall back to the live tip's own value
            // instead of returning a degenerate (0,0) range.
            if (_liveTip.TryGetValue(metricId, out var tip))
                return (tip.Value - 1, tip.Value + 1);

            return (0, 1);
        }
    }
}
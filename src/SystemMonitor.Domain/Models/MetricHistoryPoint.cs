namespace SystemMonitor.Domain.Models;

/// <summary>
/// One sample in a metric's history. NormalizedValue is null while the point
/// is still the "live tip" (the newest, still-changeable sample) and gets
/// frozen exactly once — at the moment the point graduates from live to
/// history — by IMetricHistoryStore. Once set, it is never recomputed. That
/// freeze is what keeps past points visually locked in place regardless of
/// what the live tip or later samples do.
/// </summary>
public readonly record struct MetricHistoryPoint(DateTime Timestamp, double Value, double? NormalizedValue = null);
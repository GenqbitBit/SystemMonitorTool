namespace SystemMonitor.Domain.Models;

public readonly record struct MetricHistoryPoint(DateTime Timestamp, double Value);
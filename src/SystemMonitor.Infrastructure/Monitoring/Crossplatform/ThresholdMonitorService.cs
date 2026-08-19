using System.Collections.Generic;
using System.Linq;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.CrossPlatform;

/// <summary>
/// Compares live metric readings against configured warning/critical
/// thresholds and logs a "threshold_crossed" event the moment a metric
/// enters the critical range — not on every tick it stays there, so a
/// sustained high-CPU period produces one event, not hundreds.
///
/// Platform-neutral: works purely off MetricReading values already
/// produced by IMetricsSnapshotProvider, so it lives in Crossplatform
/// alongside DotNetOsMonitorService rather than under Windows/Linux/MacOS.
/// </summary>
public sealed class ThresholdMonitorService : IThresholdMonitorService
{
    private readonly IEventLogService _eventLog;
    private readonly Dictionary<string, MetricThreshold> _thresholds;
    private readonly HashSet<string> _currentlyOverThreshold = new();

    public ThresholdMonitorService(IEventLogService eventLog, IEnumerable<MetricThreshold> thresholds)
    {
        _eventLog = eventLog;
        _thresholds = thresholds.ToDictionary(t => t.MetricId);
    }

    public void Check(IReadOnlyList<MetricReading> snapshot)
    {
        foreach (var reading in snapshot)
        {
            if (!_thresholds.TryGetValue(reading.Id, out var threshold)) continue;

            bool isOver = reading.Value >= threshold.CriticalValue;
            bool wasOver = _currentlyOverThreshold.Contains(reading.Id);

            if (isOver && !wasOver)
            {
                _eventLog.LogEvent(
                    EventType.ThresholdCrossed,
                    $"{reading.Id} crossed {threshold.CriticalValue} (value: {reading.Value})");
                _currentlyOverThreshold.Add(reading.Id);
            }
            else if (!isOver && wasOver)
            {
                _currentlyOverThreshold.Remove(reading.Id);
            }
        }
    }
}
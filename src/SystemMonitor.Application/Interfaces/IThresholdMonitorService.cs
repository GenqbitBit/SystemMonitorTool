using System.Collections.Generic;
using SystemMonitor.Domain.Models;

public interface IThresholdMonitorService
{
    void Check(IReadOnlyList<MetricReading> snapshot);
}
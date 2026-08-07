using System;
using System.Collections.Generic;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Pure math for mapping a metric's history into normalized pixel-space
/// points. Deliberately Avalonia-free (plain doubles, no Point/Rect) so
/// it can be unit tested without a rendering context. MetricGraphView
/// is the only caller — it converts the (X, Y) tuples returned here
/// into Avalonia Points when building the line geometry.
/// </summary>
public static class MetricGraphMath
{
    public static (double Min, double Max) GetValueRange(
        IReadOnlyList<MetricHistoryPoint> history, double? fixedMin = null, double? fixedMax = null)
    {
        if (fixedMin.HasValue && fixedMax.HasValue)
            return (fixedMin.Value, fixedMax.Value);

        var min = double.MaxValue;
        var max = double.MinValue;

        foreach (var point in history)
        {
            if (point.Value < min) min = point.Value;
            if (point.Value > max) max = point.Value;
        }

        if (Math.Abs(max - min) < 0.0001)
        {
            min -= 1;
            max += 1;
        }

        return (min, max);
    }

    public static IReadOnlyList<(double X, double Y)> ComputePoints(
        IReadOnlyList<MetricHistoryPoint> history, double width, double height,
        double? fixedMin = null, double? fixedMax = null)
    {
        if (history.Count < 2 || width <= 0 || height <= 0)
            return Array.Empty<(double, double)>();

        var (minValue, maxValue) = GetValueRange(history, fixedMin, fixedMax);
        var valueRange = maxValue - minValue;

        var minTime = history[0].Timestamp;
        var maxTime = history[^1].Timestamp;
        var timeSpanSeconds = (maxTime - minTime).TotalSeconds;
        if (timeSpanSeconds <= 0) timeSpanSeconds = 1;

        var points = new (double X, double Y)[history.Count];
        for (int i = 0; i < history.Count; i++)
        {
            var point = history[i];
            var x = (point.Timestamp - minTime).TotalSeconds / timeSpanSeconds * width;
            var normalized = (point.Value - minValue) / valueRange;
            var y = height - (normalized * height);
            points[i] = (x, y);
        }

        return points;
    }
}
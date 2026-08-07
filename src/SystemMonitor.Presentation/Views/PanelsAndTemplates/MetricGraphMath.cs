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
    /// <summary>
    /// Returns (min, max) across all points. If every value is
    /// (near-)identical — e.g. an idle CPU sitting flat — the range is
    /// padded by 1 in each direction so downstream normalization never
    /// divides by ~zero.
    /// </summary>
    public static (double Min, double Max) GetValueRange(IReadOnlyList<MetricHistoryPoint> history)
    {
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

    /// <summary>
    /// Maps each history point to (x, y) pixel coordinates within a
    /// widthxheight surface. X is proportional elapsed time since the
    /// first point; Y is the value normalized against valueRange and
    /// flipped (screen Y grows downward, graphs grow upward).
    /// Returns an empty list if width/height are non-positive or fewer
    /// than 2 points are available — a line needs at least 2 points.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> ComputePoints(
        IReadOnlyList<MetricHistoryPoint> history, double width, double height)
    {
        if (history.Count < 2 || width <= 0 || height <= 0)
            return Array.Empty<(double, double)>();

        var (minValue, maxValue) = GetValueRange(history);
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
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
    public static IReadOnlyList<(double Value, double NormalizedValue)> ComputeValueAxisTicks(double min, double max, int tickCount = 5)
    {
        if (tickCount < 2) tickCount = 2;

        var range = max - min;
        if (Math.Abs(range) < 0.0001) range = 1;

        var ticks = new List<(double, double)>(tickCount);
        for (int i = 0; i < tickCount; i++)
        {
            var normalized = (double)i / (tickCount - 1);
            var value = min + normalized * range;
            ticks.Add((value, normalized));
        }
        return ticks;
    }

    public static IReadOnlyList<(double NormalizedX, string Label)> ComputeTimeAxisTicks(DateTime minTime, DateTime maxTime, int tickCount = 4)
    {
        if (tickCount < 2 || maxTime <= minTime)
            return Array.Empty<(double, string)>();

        var totalSeconds = (maxTime - minTime).TotalSeconds;
        var ticks = new List<(double, string)>(tickCount);

        for (int i = 0; i < tickCount; i++)
        {
            var normalized = (double)i / (tickCount - 1);
            var secondsAgo = totalSeconds * (1 - normalized);
            var label = secondsAgo < 1 ? "now" : $"-{secondsAgo:0}s";
            ticks.Add((normalized, label));
        }
        return ticks;
    }

    public static string FormatAxisValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.05
            ? Math.Round(value).ToString("0")
            : value.ToString("0.0");
    }

    public static (double Min, double Max) GetValueRange(
        IReadOnlyList<MetricHistoryPoint> history, double? fixedMin = null, double? fixedMax = null)
    {
        if (fixedMin.HasValue && fixedMax.HasValue)
            return (fixedMin.Value, fixedMax.Value);

        if (history.Count == 0)
            return (0, 1);

        var min = history[0].Value;
        var max = history[0].Value;

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

    public static (double Min, double Max) ResolveDisplayRange(
    (double Min, double Max) committedRange, double? fixedMin = null, double? fixedMax = null)
    {
        if (fixedMin.HasValue && fixedMax.HasValue)
            return (fixedMin.Value, fixedMax.Value);

        var (min, max) = committedRange;
        if (Math.Abs(max - min) < 0.0001)
        {
            min -= 1;
            max += 1;
        }
        return (min, max);
    }

    public static IReadOnlyList<(double X, double Y)> ComputePoints(
        IReadOnlyList<MetricHistoryPoint> history, double width, double height,
        DateTime windowStart, DateTime windowEnd,
        double? fixedMin = null, double? fixedMax = null, bool useFrozenValues = true)
    {
        var range = GetValueRange(history, fixedMin, fixedMax);
        return ComputePoints(history, width, height, windowStart, windowEnd, range.Min, range.Max, useFrozenValues);
    }

    // The auto-scale path keeps already-graduated points stable by honoring
    // the frozen NormalizedValue baked in at graduation time. Only the live tip
    // is re-normalized against the current range; the past remains immutable.
    public static IReadOnlyList<(double X, double Y)> ComputePoints(
        IReadOnlyList<MetricHistoryPoint> history, double width, double height,
        DateTime windowStart, DateTime windowEnd,
        double minValue, double maxValue, bool useFrozenValues = true)
    {
        if (width <= 0 || height <= 0 || history.Count == 0)
            return Array.Empty<(double, double)>();

        // RenderMirrored deliberately calls this with min/max swapped to flip
        // the bottom half upside-down relative to the top. Detect that so frozen
        // historical points get flipped too, not just the live tip.
        var mirrored = maxValue < minValue;
        var orderedMin = mirrored ? maxValue : minValue;
        var orderedMax = mirrored ? minValue : maxValue;
        var valueRange = orderedMax - orderedMin;
        if (Math.Abs(valueRange) < 0.0001) valueRange = 1;

        var windowSeconds = (windowEnd - windowStart).TotalSeconds;
        if (windowSeconds <= 0) windowSeconds = 1;

        var points = new (double X, double Y)[history.Count];
        for (int i = 0; i < history.Count; i++)
        {
            var point = history[i];
            var normalizedX = (point.Timestamp - windowStart).TotalSeconds / windowSeconds;
            var x = Math.Clamp(normalizedX, 0, 1) * width;

            var normalizedY = useFrozenValues && point.NormalizedValue.HasValue
                ? point.NormalizedValue.Value
                : Math.Clamp((point.Value - orderedMin) / valueRange, 0, 1);

            if (mirrored) normalizedY = 1 - normalizedY;

            var y = height - (normalizedY * height);
            points[i] = (x, y);
        }

        return points;
    }
    
}
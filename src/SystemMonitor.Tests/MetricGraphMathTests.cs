using System;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Views.PanelsAndTemplates;
using Xunit;

namespace SystemMonitor.Tests;

public class MetricGraphMathTests
{
    [Fact]
    public void ComputePoints_ReturnsEmpty_WhenHistoryIsEmpty()
    {
        var now = DateTime.UtcNow;
        var history = Array.Empty<MetricHistoryPoint>();

        var points = MetricGraphMath.ComputePoints(history, 300, 80, now.AddSeconds(-60), now);

        Assert.Empty(points);
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(300, 0)]
    [InlineData(-10, 80)]
    [InlineData(300, -10)]
    public void ComputePoints_ReturnsEmpty_WhenBoundsAreNonPositive(double width, double height)
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 10),
            new MetricHistoryPoint(now.AddSeconds(1), 20),
        };

        var points = MetricGraphMath.ComputePoints(history, width, height, now.AddSeconds(-60), now);

        Assert.Empty(points);
    }

    [Fact]
    public void GetValueRange_PadsFlatHistory_ToAvoidDivideByZero()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 42),
            new MetricHistoryPoint(now.AddSeconds(1), 42),
        };

        var (min, max) = MetricGraphMath.GetValueRange(history);

        Assert.True(max - min >= 2); // padded by 1 in each direction
        Assert.True(min < 42);
        Assert.True(max > 42);
    }

    [Fact]
    public void GetValueRange_ReturnsExactMinAndMax_ForVaryingHistory()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 10),
            new MetricHistoryPoint(now.AddSeconds(1), 55),
            new MetricHistoryPoint(now.AddSeconds(2), 30),
        };

        var (min, max) = MetricGraphMath.GetValueRange(history);

        Assert.Equal(10, min);
        Assert.Equal(55, max);
    }

    // REPLACES the old "FirstPointAnchoredAtOriginX" test. That test asserted
    // x=0 purely because the domain WAS the data span (history[0] == windowStart
    // by construction). Now a point's X position is anchored to the fixed
    // window, not to being first/last in the list — so this test proves the
    // real invariant: a sample at windowStart maps to x=0, regardless of
    // whether it's the earliest sample currently in history.
    [Fact]
    public void ComputePoints_SampleAtWindowStart_MapsToOriginX()
    {
        var windowStart = DateTime.UtcNow;
        var windowEnd = windowStart.AddSeconds(60);
        var history = new[]
        {
            new MetricHistoryPoint(windowStart, 0),
            new MetricHistoryPoint(windowStart.AddSeconds(30), 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80, windowStart, windowEnd);

        Assert.Equal(0, points[0].X, precision: 3);
    }

    // REPLACES the old "LastPointAnchoredAtFullWidth" test, same reasoning:
    // a sample AT windowEnd maps to x=width because of its absolute timestamp,
    // not because it's last in the list.
    [Fact]
    public void ComputePoints_SampleAtWindowEnd_MapsToFullWidth()
    {
        var windowStart = DateTime.UtcNow;
        var windowEnd = windowStart.AddSeconds(60);
        var history = new[]
        {
            new MetricHistoryPoint(windowStart, 0),
            new MetricHistoryPoint(windowEnd, 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80, windowStart, windowEnd);

        Assert.Equal(300, points[^1].X, precision: 3);
    }

    // NEW: proves the core scrolling behavior — a partially-filled window
    // (data doesn't yet span the full 60s) leaves the unfilled portion of
    // the window empty rather than stretching the available data to fill
    // the whole plot width. This is the actual bug being fixed.
    [Fact]
    public void ComputePoints_PartiallyFilledWindow_DoesNotStretchDataToFillWidth()
    {
        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddSeconds(-60);
        // Only 2 seconds of data exist so far, near the right edge of a 60s window.
        var history = new[]
        {
            new MetricHistoryPoint(windowEnd.AddSeconds(-2), 0),
            new MetricHistoryPoint(windowEnd, 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80, windowStart, windowEnd);

        // 2s into a 60s window at 300px width => roughly (58/60)*300 ≈ 290px in from the left.
        Assert.True(points[0].X > 280, $"expected first point pinned near the right edge, got X={points[0].X}");
        Assert.Equal(300, points[^1].X, precision: 3);
    }

    // NEW: proves stability — the SAME sample's X position must not change
    // just because more samples arrived later, as long as the window itself
    // (windowStart/windowEnd) is held fixed. This is what stops the
    // "squeeze" effect: a point's position depends only on its own
    // timestamp vs. the window, never on sibling count.
    [Fact]
    public void ComputePoints_ExistingSamplePosition_IsStable_WhenNewSamplesArrive()
    {
        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddSeconds(-60);
        var fixedTimestamp = windowEnd.AddSeconds(-30);

        var historyBefore = new[]
        {
            new MetricHistoryPoint(fixedTimestamp, 50),
        };
        var historyAfter = new[]
        {
            new MetricHistoryPoint(fixedTimestamp, 50),
            new MetricHistoryPoint(windowEnd, 75), // a new sample arrives
        };

        var xBefore = MetricGraphMath.ComputePoints(historyBefore, 300, 80, windowStart, windowEnd)[0].X;
        var xAfter = MetricGraphMath.ComputePoints(historyAfter, 300, 80, windowStart, windowEnd)[0].X;

        Assert.Equal(xBefore, xAfter, precision: 3);
    }

    [Fact]
    public void ComputePoints_HighestValue_MapsToSmallestY()
    {
        // Screen Y grows downward, so the highest metric value should
        // produce the smallest Y (drawn nearest the top of the control).
        var windowStart = DateTime.UtcNow;
        var windowEnd = windowStart.AddSeconds(60);
        var history = new[]
        {
            new MetricHistoryPoint(windowStart, 0),
            new MetricHistoryPoint(windowStart.AddSeconds(1), 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80, windowStart, windowEnd);

        Assert.Equal(80, points[0].Y, precision: 3);  // value 0   -> bottom
        Assert.Equal(0, points[1].Y, precision: 3);   // value 100 -> top
    }

    [Fact]
    public void ComputePoints_MidpointTimestamp_MapsToHalfWidth_WhenWindowMatchesDataSpan()
    {
        var windowStart = DateTime.UtcNow;
        var windowEnd = windowStart.AddSeconds(20);
        var history = new[]
        {
            new MetricHistoryPoint(windowStart, 0),
            new MetricHistoryPoint(windowStart.AddSeconds(10), 50),
            new MetricHistoryPoint(windowEnd, 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80, windowStart, windowEnd);

        Assert.Equal(150, points[1].X, precision: 3);
    }

    [Fact]
    public void ComputePoints_ReturnsOnePointPerHistoryEntry()
    {
        var windowStart = DateTime.UtcNow;
        var windowEnd = windowStart.AddSeconds(60);
        var history = new[]
        {
            new MetricHistoryPoint(windowStart, 1),
            new MetricHistoryPoint(windowStart.AddSeconds(1), 2),
            new MetricHistoryPoint(windowStart.AddSeconds(2), 3),
            new MetricHistoryPoint(windowStart.AddSeconds(3), 4),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80, windowStart, windowEnd);

        Assert.Equal(history.Length, points.Count);
    }

    [Fact]
    public void GetValueRange_UsesFixedRange_WhenBothProvided()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 0.1),
            new MetricHistoryPoint(now.AddSeconds(1), 2.1),
        };

        var (min, max) = MetricGraphMath.GetValueRange(history, fixedMin: 0, fixedMax: 100);

        Assert.Equal(0, min);
        Assert.Equal(100, max);
    }

    [Fact]
    public void ComputePoints_FixedRange_ProducesSmallVisualMovement_ForSmallValueChange()
    {
        // The exact scenario from the bug report: 0.1% -> 2.1% should barely
        // move on a 0-100 scale, not look like a dramatic swing.
        var windowStart = DateTime.UtcNow;
        var windowEnd = windowStart.AddSeconds(60);
        var history = new[]
        {
            new MetricHistoryPoint(windowStart, 0.1),
            new MetricHistoryPoint(windowStart.AddSeconds(1), 2.1),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 100, windowStart, windowEnd, fixedMin: 0, fixedMax: 100);

        var yMovement = Math.Abs(points[1].Y - points[0].Y);
        Assert.True(yMovement < 5); // 2% of a 100px-tall graph is ~2px
    }

    [Theory]
    [InlineData(119, 40)]
    [InlineData(100, 31)]
    public void BlockAreaGraphRenderer_UsesLineFallback_ForSmallPlots(double width, double height)
    {
        Assert.True(BlockAreaGraphRenderer.ShouldUseLineFallback(width, height));
    }

    [Theory]
    [InlineData(120, 40)]
    [InlineData(130, 32)]
    public void BlockAreaGraphRenderer_UsesBlockRenderer_ForNormalSizedPlots(double width, double height)
    {
        Assert.False(BlockAreaGraphRenderer.ShouldUseLineFallback(width, height));
    }
}
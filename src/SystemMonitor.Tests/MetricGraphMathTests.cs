using System;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Views.PanelsAndTemplates;
using Xunit;

namespace SystemMonitor.Tests;

public class MetricGraphMathTests
{
    [Fact]
    public void ComputePoints_ReturnsEmpty_WhenFewerThanTwoPoints()
    {
        var history = new[] { new MetricHistoryPoint(DateTime.UtcNow, 50) };

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

        Assert.Empty(points);
    }

    [Fact]
    public void ComputePoints_ReturnsEmpty_WhenHistoryIsEmpty()
    {
        var history = Array.Empty<MetricHistoryPoint>();

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

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

        var points = MetricGraphMath.ComputePoints(history, width, height);

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

    [Fact]
    public void ComputePoints_FirstPointAnchoredAtOriginX()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 0),
            new MetricHistoryPoint(now.AddSeconds(30), 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

        Assert.Equal(0, points[0].X, precision: 3);
    }

    [Fact]
    public void ComputePoints_LastPointAnchoredAtFullWidth()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 0),
            new MetricHistoryPoint(now.AddSeconds(30), 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

        Assert.Equal(300, points[^1].X, precision: 3);
    }

    [Fact]
    public void ComputePoints_HighestValue_MapsToSmallestY()
    {
        // Screen Y grows downward, so the highest metric value should
        // produce the smallest Y (drawn nearest the top of the control).
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 0),
            new MetricHistoryPoint(now.AddSeconds(1), 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

        Assert.Equal(80, points[0].Y, precision: 3);  // value 0   -> bottom
        Assert.Equal(0, points[1].Y, precision: 3);   // value 100 -> top
    }

    [Fact]
    public void ComputePoints_MidpointTimestamp_MapsToHalfWidth()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 0),
            new MetricHistoryPoint(now.AddSeconds(10), 50),
            new MetricHistoryPoint(now.AddSeconds(20), 100),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

        Assert.Equal(150, points[1].X, precision: 3);
    }

    [Fact]
    public void ComputePoints_ReturnsOnePointPerHistoryEntry()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 1),
            new MetricHistoryPoint(now.AddSeconds(1), 2),
            new MetricHistoryPoint(now.AddSeconds(2), 3),
            new MetricHistoryPoint(now.AddSeconds(3), 4),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 80);

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
        var now = DateTime.UtcNow;
        var history = new[]
        {
            new MetricHistoryPoint(now, 0.1),
            new MetricHistoryPoint(now.AddSeconds(1), 2.1),
        };

        var points = MetricGraphMath.ComputePoints(history, 300, 100, fixedMin: 0, fixedMax: 100);

        var yMovement = Math.Abs(points[1].Y - points[0].Y);
        Assert.True(yMovement < 5); // 2% of a 100px-tall graph is ~2px
    }
}
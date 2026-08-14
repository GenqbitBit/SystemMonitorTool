using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class BlockAreaGraphRenderer : IGraphContentRenderer
{
    public double BlockWidth { get; set; } = 2;
    public double BlockGap { get; set; } = 1;
    public Color TopColor { get; set; } = Colors.MediumPurple;
    public Color BottomColor { get; set; } = Colors.Indigo;
    public int BandCount { get; set; } = 5;

    public double SmallPlotWidthThreshold { get; set; } = 120;
    public double SmallPlotHeightThreshold { get; set; } = 32;

    public static bool ShouldUseLineFallback(double width, double height)
        => width < 120 || height < 32;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true)
    {
        if (plotRect.Width < SmallPlotWidthThreshold || plotRect.Height < SmallPlotHeightThreshold)
        {
            DrawLineFallback(context, plotRect, history, minValue, maxValue, windowStart, windowEnd, useFrozenValues);
            return;
        }

        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, windowStart, windowEnd, minValue, maxValue, useFrozenValues);
        if (points.Count == 0)
            return;

        // Nothing real to draw before the first sample's X — skip those
        // columns instead of letting InterpolateYAt flat-extrapolate the
        // first value backward into the empty part of the window.
        var firstDataX = points[0].X;

        var step = BlockWidth + BlockGap;
        var columnCount = (int)(plotRect.Width / step);
        const double overlap = 0.75;

        for (int col = 0; col <= columnCount; col++)
        {
            var x = col * step;
            if (x > plotRect.Width) break;
            if (x + BlockWidth < firstDataX) continue;

            var y = BlockGraphMath.InterpolateYAt(points, x);
            var curveY = plotRect.Y + y;

            double fillTop, fillBottom;
            if (baselineAtTop)
            {
                fillTop = plotRect.Y;
                fillBottom = curveY;
            }
            else
            {
                fillTop = curveY;
                fillBottom = plotRect.Y + plotRect.Height;
            }

            var fillHeight = fillBottom - fillTop;
            if (fillHeight <= 0)
                continue;

            var bandHeight = fillHeight / BandCount;

            for (int b = 0; b < BandCount; b++)
            {
                var bandTop = fillTop + b * bandHeight;
                var bandCenter = bandTop + bandHeight / 2;
                var t = Math.Abs(bandCenter - curveY) / fillHeight; // 0 near curve, 1 near baseline
                var color = GraphColorMath.Lerp(TopColor, BottomColor, t);

                var drawHeight = bandHeight + (b < BandCount - 1 ? overlap : 0);
                context.FillRectangle(new SolidColorBrush(color),
                    new Rect(plotRect.X + x, bandTop, BlockWidth, drawHeight));
            }
        }
    }

    private void DrawLineFallback(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd, bool useFrozenValues)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, windowStart, windowEnd, minValue, maxValue, useFrozenValues);
        if (points.Count == 0)
            return;

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(plotRect.X + points[0].X, plotRect.Y + points[0].Y), isFilled: false);
            for (int i = 1; i < points.Count; i++)
                stream.LineTo(new Point(plotRect.X + points[i].X, plotRect.Y + points[i].Y));
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(TopColor), 1.2), geometry);
    }
}
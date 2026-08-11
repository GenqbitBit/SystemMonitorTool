using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class BlockAreaGraphRenderer : IGraphContentRenderer
{
    public double BlockWidth { get; set; } = 15;
    public double BlockGap { get; set; } = 1;
    public Color TopColor { get; set; } = Colors.MediumPurple;
    public Color BottomColor { get; set; } = Colors.Indigo;
    public int BandCount { get; set; } = 5;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, bool baselineAtTop = false)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, minValue, maxValue);
        if (points.Count == 0)
            return;

        var step = BlockWidth + BlockGap;
        var columnCount = (int)(plotRect.Width / step);
        const double overlap = 0.75;

        for (int col = 0; col <= columnCount; col++)
        {
            var x = col * step;
            if (x > plotRect.Width) break;

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
}
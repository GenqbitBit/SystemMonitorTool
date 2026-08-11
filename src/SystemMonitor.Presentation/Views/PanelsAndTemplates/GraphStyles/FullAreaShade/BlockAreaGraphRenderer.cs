using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class BlockAreaGraphRenderer : IGraphContentRenderer
{
    public double BlockWidth { get; set; } = 4;
    public double BlockGap { get; set; } = 1;

    public Color TopColor { get; set; } = Colors.MediumPurple;
    public Color BottomColor { get; set; } = Colors.Indigo;

    // Bands are computed relative to EACH bar's own rendered height, not the
    // full plot height — so even a short bar shows the full color range.
    public int BandCount { get; set; } = 8;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, minValue, maxValue);
        if (points.Count == 0)
            return;

        var step = BlockWidth + BlockGap;
        var columnCount = (int)(plotRect.Width / step);

        for (int col = 0; col <= columnCount; col++)
        {
            var x = col * step;
            if (x > plotRect.Width) break;

            var y = BlockGraphMath.InterpolateYAt(points, x);
            var top = plotRect.Y + y;
            var bottom = plotRect.Y + plotRect.Height;
            var fillHeight = bottom - top;
            if (fillHeight <= 0)
                continue;

            var bandHeight = fillHeight / BandCount;
            const double overlap = 0.75; // px of intentional overdraw to hide AA seams

            for (int b = 0; b < BandCount; b++)
            {
                var bandTop = top + b * bandHeight;
                var localT = (b + 0.5) / BandCount;
                var color = GraphColorMath.Lerp(TopColor, BottomColor, localT);

                // extend height by `overlap` so this band's edge is drawn UNDER the
                // next band's top edge, instead of two AA'd edges meeting hairline-thin
                var drawHeight = bandHeight + (b < BandCount - 1 ? overlap : 0);

                context.FillRectangle(new SolidColorBrush(color),
                    new Rect(plotRect.X + x, bandTop, BlockWidth, drawHeight));
            }
        }
    }
}
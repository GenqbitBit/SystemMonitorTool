using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class BlockAreaGraphRenderer : IGraphContentRenderer
{
    public IBrush BlockBrush { get; set; } = Brushes.LimeGreen;
    public double BlockWidth { get; set; } = 4;
    public double BlockGap { get; set; } = 1;

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

            context.FillRectangle(BlockBrush, new Rect(plotRect.X + x, top, BlockWidth, bottom - top));
        }
    }
}
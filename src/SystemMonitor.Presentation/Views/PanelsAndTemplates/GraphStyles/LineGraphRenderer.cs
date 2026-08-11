using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// The original/default graph style: a continuous stroked line through
/// each history point. Extracted out of MetricGraphView unchanged so it
/// can sit behind IGraphContentRenderer alongside future styles.
/// </summary>
public class LineGraphRenderer : IGraphContentRenderer
{
    public IBrush LineBrush { get; set; } = Brushes.LimeGreen;
    public double LineThickness { get; set; } = 1.5;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, minValue, maxValue);
        if (points.Count == 0)
            return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(plotRect.X + points[0].X, plotRect.Y + points[0].Y), isFilled: false);
            for (int i = 1; i < points.Count; i++)
                ctx.LineTo(new Point(plotRect.X + points[i].X, plotRect.Y + points[i].Y));
        }
        context.DrawGeometry(null, new Pen(LineBrush, LineThickness), geometry);
    }
}
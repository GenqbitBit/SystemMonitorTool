using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

// A static 0-100% progress bar for percentage metrics — NOT a history graph.
// Always renders the full fixed-width bar; ignores windowStart/windowEnd,
// minValue/maxValue, and useFrozenValues entirely, and never calls
// MetricGraphMath.ComputePoints. Only the latest sample matters.
public class BlockPercentageBarRenderer : IGraphContentRenderer
{
    public double BlockWidth { get; set; } = 2;
    public double BlockGap { get; set; } = 1;
    public Color StartColor { get; set; } = Colors.Red;       // color at 0% (left)
    public Color EndColor { get; set; } = Colors.LimeGreen;   // color at 100% (right)
    public double UnfilledOpacity { get; set; } = 0.2;

    public IBrush MarkerBrush { get; set; } = Brushes.White;
    public double MarkerFlapWidth { get; set; } = 6;
    public double MarkerFlapHeight { get; set; } = 5;
    public double MarkerLineThickness { get; set; } = 1.7;

    // Draws its own tip marker (below) instead of MetricGraphView's default
    // dot+value-label overlay, which assumes a vertical time-series mapping
    // this renderer doesn't use.
    public bool SuppressDefaultCurrentValueMarker => true;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true, bool toRight = true)
    {
        if (history.Count == 0)
            return;

        var currentValue = Math.Clamp(history[^1].Value, 0, 100);

        var step = BlockWidth + BlockGap;
        var columnCount = (int)((plotRect.Width + BlockGap) / step);

        for (int col = 0; col < columnCount; col++)
        {
            var x = col * step;
            if (x + BlockWidth > plotRect.Width)
                break;

            var colPct = (x + BlockWidth / 2) / plotRect.Width * 100;
            if (!toRight) colPct = 100 - colPct;
            var t = Math.Clamp(colPct / 100.0, 0, 1);
            var color = GraphColorMath.Lerp(StartColor, EndColor, t);

            var isFilled = colPct <= currentValue;
            var drawColor = isFilled ? color : WithOpacity(color, UnfilledOpacity);

            context.FillRectangle(new SolidColorBrush(drawColor), new Rect(plotRect.X + x, plotRect.Y, BlockWidth, plotRect.Height));
        }

        DrawTipMarker(context, plotRect, currentValue, toRight);
    }

    private void DrawTipMarker(DrawingContext context, Rect plotRect, double currentValue, bool toRight)
    {
        var fraction = currentValue / 100.0;
        if (!toRight) fraction = 1 - fraction;
        var tipX = plotRect.X + fraction * plotRect.Width;
        context.DrawLine(new Pen(MarkerBrush, MarkerLineThickness),
            new Point(tipX, plotRect.Y), new Point(tipX, plotRect.Y + plotRect.Height));

        var flapHeight = Math.Min(MarkerFlapHeight, plotRect.Height);
        context.FillRectangle(MarkerBrush,
            new Rect(tipX - MarkerFlapWidth / 2, plotRect.Y, MarkerFlapWidth, flapHeight));
    }
    
    private static Color WithOpacity(Color color, double opacity)
    {
        var alpha = (byte)(color.A * Math.Clamp(opacity, 0, 1));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
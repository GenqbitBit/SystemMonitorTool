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
        bool baselineAtTop = false, bool useFrozenValues = true)
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

            // Percentage represented by THIS column's fixed position along the
            // bar — a plain 0-100 mapping across plotRect.Width, not derived
            // from any history shape.
            var colPct = (x + BlockWidth / 2) / plotRect.Width * 100;
            var t = Math.Clamp(colPct / 100.0, 0, 1);
            var color = GraphColorMath.Lerp(StartColor, EndColor, t);

            var isFilled = colPct <= currentValue;
            // Alpha is baked directly into the Color rather than left to
            // Brush.Opacity, so the dimming is guaranteed regardless of any
            // other opacity/compositing state in the render tree.
            var drawColor = isFilled ? color : WithOpacity(color, UnfilledOpacity);

            context.FillRectangle(new SolidColorBrush(drawColor), new Rect(plotRect.X + x, plotRect.Y, BlockWidth, plotRect.Height));
        }

        DrawTipMarker(context, plotRect, currentValue);
    }

    // A precise marker at the EXACT tip position, independent of block-width
    // quantization. Drawn as a line through the bar plus a small cap flush
    // with the TOP of plotRect — not above it. MetricGraphView clips renderer
    // output to plotRect (see RenderSingle's PushClip), so anything drawn
    // above plotRect.Y is invisible no matter how large it's made.
    private void DrawTipMarker(DrawingContext context, Rect plotRect, double currentValue)
    {
        var tipX = plotRect.X + currentValue / 100.0 * plotRect.Width;
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
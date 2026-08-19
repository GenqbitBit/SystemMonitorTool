using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

// A static progress bar for percentage-like metrics — NOT a history graph.
// Always renders the full fixed-width bar; ignores windowStart/windowEnd,
// minValue/maxValue params, and useFrozenValues entirely, and never calls
// MetricGraphMath.ComputePoints. Only the latest sample matters.
public class BlockPercentageBarRenderer : IGraphContentRenderer, IThemeableGraphRenderer
{
    public double BlockWidth { get; set; } = 2;
    public double BlockGap { get; set; } = 1;
    public Color StartColor { get; set; } = Colors.Red;       // color at MinValue (left)
    public Color EndColor { get; set; } = Colors.LimeGreen;   // color at MaxValue (right)
    public double UnfilledOpacity { get; set; } = 0.2;

    // Overridable value range — strictly 0-100% unless explicitly changed.
    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 100;

    public IBrush MarkerBrush { get; set; } = Brushes.White;
    public double MarkerFlapWidth { get; set; } = 6;
    public double MarkerFlapHeight { get; set; } = 5;
    public double MarkerLineThickness { get; set; } = 1.7;

    public bool MarkerFlapAtTop { get; set; } = true;


    public bool SuppressDefaultCurrentValueMarker => true;

    public void ApplyPalette(Color primary, Color secondary)
    {
        StartColor = primary;
        EndColor = secondary;
    }
    
    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true, bool toRight = true)
    {
        if (history.Count == 0)
            return;

        var range = MaxValue - MinValue;
        var currentValue = Math.Clamp(history[^1].Value, MinValue, MaxValue);
        // Position of the current value along the bar, expressed as a 0-100
        // percentage of MinValue..MaxValue — kept separate from the raw
        // value so gradient/fill math below stays range-agnostic.
        var currentValuePct = range > 0 ? (currentValue - MinValue) / range * 100 : 0;

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

            var isFilled = colPct <= currentValuePct;
            var drawColor = isFilled ? color : WithOpacity(color, UnfilledOpacity);

            context.FillRectangle(new SolidColorBrush(drawColor), new Rect(plotRect.X + x, plotRect.Y, BlockWidth, plotRect.Height));
        }

        DrawTipMarker(context, plotRect, currentValuePct, toRight);
    }

    private void DrawTipMarker(DrawingContext context, Rect plotRect, double currentValuePct, bool toRight)
    {
        var fraction = currentValuePct / 100.0;
        if (!toRight) fraction = 1 - fraction;
        var tipX = plotRect.X + fraction * plotRect.Width;
        context.DrawLine(new Pen(MarkerBrush, MarkerLineThickness),
            new Point(tipX, plotRect.Y), new Point(tipX, plotRect.Y + plotRect.Height));

        var flapHeight = Math.Min(MarkerFlapHeight, plotRect.Height);
        var flapY = MarkerFlapAtTop ? plotRect.Y : plotRect.Y + plotRect.Height - flapHeight;
        context.FillRectangle(MarkerBrush,
            new Rect(tipX - MarkerFlapWidth / 2, flapY, MarkerFlapWidth, flapHeight));
    }
    
    private static Color WithOpacity(Color color, double opacity)
    {
        var alpha = (byte)(color.A * Math.Clamp(opacity, 0, 1));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
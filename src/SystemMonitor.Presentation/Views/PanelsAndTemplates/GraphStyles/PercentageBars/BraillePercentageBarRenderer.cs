using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

// Braille-texture counterpart to BlockPercentageBarRenderer — same static
// progress-bar behavior (no history/axes, only the latest sample),
// rendered with solid braille cells (all 8 dots lit) instead of blocks.
public class BraillePercentageBarRenderer : IGraphContentRenderer, IThemeableGraphRenderer
{
    public double CellWidth { get; set; } = 15;
    public double CellHeight { get; set; } = 10;
    public double FontSize { get; set; } = 20;
    public FontFamily FontFamily { get; set; } = AppFonts.Monospace;
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
    private readonly Dictionary<Color, SolidColorBrush> _brushes = new();

    public bool SuppressDefaultCurrentValueMarker => true;

    // All 8 braille dots lit — reuses BrailleGraphMath.ToBrailleChar to get a
    // solid-looking cell, same conventions as the area renderer's dot cells.
    private const byte FullCellMask = 0xFF;

    public void ApplyPalette(Color primary, Color secondary)
    {
        StartColor = primary;
        EndColor = secondary;
        _brushes.Clear();
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

        var typeface = new Typeface(FontFamily);

        var columnCount = (int)(plotRect.Width / CellWidth);
        var rowCount = (int)(plotRect.Height / CellHeight);
        var fullCellChar = BrailleGraphMath.ToBrailleChar(FullCellMask).ToString();

        for (int row = 0; row < rowCount; row++)
        {
            var cellY = row * CellHeight;

            for (int col = 0; col < columnCount; col++)
            {
                var cellX = col * CellWidth;

                // Percentage represented by THIS cell's fixed position along
                // the bar. Mirrored when toRight is false so 100% anchors to
                // plotRect.X instead of the far edge — keeps the gradient and
                // fill direction a true mirror image of the toRight=true case.
                var colPct = (cellX + CellWidth / 2) / plotRect.Width * 100;
                if (!toRight) colPct = 100 - colPct;
                var t = Math.Clamp(colPct / 100.0, 0, 1);
                var color = GraphColorMath.Lerp(StartColor, EndColor, t);

                var isFilled = colPct <= currentValuePct;
                // Alpha baked directly into the Color (not Brush.Opacity) so
                // dimming is guaranteed regardless of other compositing state.
                var drawColor = isFilled ? color : WithOpacity(color, UnfilledOpacity);
                var brush = GetBrush(drawColor);

                var text = new FormattedText(fullCellChar, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, FontSize, brush);
                context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
            }
        }

        DrawTipMarker(context, plotRect, currentValuePct, toRight);
    }

    // Same precise, non-quantized tip marker as the block variant — drawn
    // flush with the TOP of plotRect (not above it), since MetricGraphView
    // clips renderer output to plotRect and anything above plotRect.Y is
    // invisible no matter how large MarkerFlapHeight is set. Mirrored when
    // toRight is false to match the mirrored fill above.
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

    private SolidColorBrush GetBrush(Color color)
    {
        if (_brushes.TryGetValue(color, out var brush))
            return brush;

        brush = new SolidColorBrush(color);
        _brushes[color] = brush;
        return brush;
    }
}
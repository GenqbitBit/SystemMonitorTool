using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

// Braille-texture counterpart to BlockPercentageBarRenderer — same static
// 0-100% progress-bar behavior (no history/axes, only the latest sample),
// rendered with solid braille cells (all 8 dots lit) instead of blocks.
public class BraillePercentageBarRenderer : IGraphContentRenderer
{
    public double CellWidth { get; set; } = 15;
    public double CellHeight { get; set; } = 10;
    public double FontSize { get; set; } = 20;
    public FontFamily FontFamily { get; set; } = AppFonts.Monospace;
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

    // All 8 braille dots lit — reuses BrailleGraphMath.ToBrailleChar to get a
    // solid-looking cell, same conventions as the area renderer's dot cells.
    private const byte FullCellMask = 0xFF;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true)
    {
        if (history.Count == 0)
            return;

        var currentValue = Math.Clamp(history[^1].Value, 0, 100);
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

                var colPct = (cellX + CellWidth / 2) / plotRect.Width * 100;
                var t = Math.Clamp(colPct / 100.0, 0, 1);
                var color = GraphColorMath.Lerp(StartColor, EndColor, t);

                var isFilled = colPct <= currentValue;
                // Alpha baked directly into the Color (not Brush.Opacity) so
                // dimming is guaranteed regardless of other compositing state.
                var drawColor = isFilled ? color : WithOpacity(color, UnfilledOpacity);
                var brush = new SolidColorBrush(drawColor);

                var text = new FormattedText(fullCellChar, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, FontSize, brush);
                context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
            }
        }

        DrawTipMarker(context, plotRect, currentValue);
    }

    // Same precise, non-quantized tip marker as the block variant — drawn
    // flush with the TOP of plotRect (not above it), since MetricGraphView
    // clips renderer output to plotRect and anything above plotRect.Y is
    // invisible no matter how large MarkerFlapHeight is set.
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
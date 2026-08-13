using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class BrailleAreaGraphRenderer : IGraphContentRenderer
{
    public double CellWidth { get; set; } = 20;
    public double CellHeight { get; set; } = 10;
    public double FontSize { get; set; } = 20;
    public FontFamily FontFamily { get; set; } = AppFonts.Monospace;

    public Color CurveTopColor { get; set; } = Colors.LimeGreen;
    public Color CurveBottomColor { get; set; } = Colors.DarkGreen;
    public Color AreaTopColor { get; set; } = Colors.MediumPurple;
    public Color AreaBottomColor { get; set; } = Colors.Indigo;
    public int BandCount { get; set; } = 8;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, windowStart, windowEnd, minValue, maxValue, useFrozenValues);
        if (points.Count == 0)
            return;

        var typeface = new Typeface(FontFamily);

        foreach (var (cellX, cellY, mask) in BrailleGraphMath.ComputeBrailleAreaCells(
                     points, plotRect.Width, plotRect.Height, CellWidth, CellHeight, baselineAtTop))
        {
            var sampleX = cellX + CellWidth / 2;
            var curveY = BlockGraphMath.InterpolateYAt(points, sampleX);

            double t;
            if (baselineAtTop)
            {
                var localSpan = curveY; // distance from baseline(0) to curve
                t = localSpan > 0.0001 ? (curveY - cellY) / localSpan : 1.0;
            }
            else
            {
                var localSpan = plotRect.Height - curveY; // distance from curve to baseline(height)
                t = localSpan > 0.0001 ? (cellY - curveY) / localSpan : 1.0;
            }

            var rowBrush = new SolidColorBrush(GraphColorMath.Band(AreaTopColor, AreaBottomColor, t, BandCount));
            var text = new FormattedText(BrailleGraphMath.ToBrailleChar(mask).ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, rowBrush);
            context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
        }

        foreach (var (cellX, cellY, mask) in BrailleGraphMath.ComputeBrailleCells(
                     points, plotRect.Width, plotRect.Height, CellWidth, CellHeight))
        {
            var t = cellY / plotRect.Height;
            var rowBrush = new SolidColorBrush(GraphColorMath.Lerp(CurveTopColor, CurveBottomColor, t));
            var text = new FormattedText(BrailleGraphMath.ToBrailleChar(mask).ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, rowBrush);
            context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
        }
    }
}
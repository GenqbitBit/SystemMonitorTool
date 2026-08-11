using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class BrailleAreaGraphRenderer : IGraphContentRenderer
{
    public double CellWidth { get; set; } = 6;
    public double CellHeight { get; set; } = 12;
    public double FontSize { get; set; } = 10;
    public FontFamily FontFamily { get; set; } = new FontFamily("Consolas");

    public Color CurveTopColor { get; set; } = Colors.LimeGreen;
    public Color CurveBottomColor { get; set; } = Colors.DarkGreen;
    public Color AreaTopColor { get; set; } = Colors.MediumPurple;
    public Color AreaBottomColor { get; set; } = Colors.Indigo;

    // Bands are computed relative to each column's OWN curve-to-floor span,
    // not the full plot height.
    public int BandCount { get; set; } = 8;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, minValue, maxValue);
        if (points.Count == 0)
            return;

        var typeface = new Typeface(FontFamily);

        // Fill drawn first so the curve sits visually on top of it.
        foreach (var (cellX, cellY, mask) in BrailleGraphMath.ComputeBrailleAreaCells(
                     points, plotRect.Width, plotRect.Height, CellWidth, CellHeight))
        {
            // Sample the curve's own height at this column so t is LOCAL
            // to this bar's fill span (curveY..plotRect.Height), not global.
            var sampleX = cellX + CellWidth / 2;
            var curveY = BlockGraphMath.InterpolateYAt(points, sampleX);
            var localSpan = plotRect.Height - curveY;

            var t = localSpan > 0.0001 ? (cellY - curveY) / localSpan : 1.0;
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
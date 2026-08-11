using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Braille-dot curve with the area beneath it filled, same idea as
/// DotAreaGraphRenderer but using BrailleGraphMath's higher sub-dot
/// density for both the curve and the fill. Independent of
/// BrailleGraphRenderer — no shared base class, curve-only style keeps
/// working unchanged.
/// </summary>
public class BrailleAreaGraphRenderer : IGraphContentRenderer
{
    public IBrush CurveBrush { get; set; } = Brushes.LimeGreen;
    public IBrush AreaBrush { get; set; } = new SolidColorBrush(Color.FromArgb(90, 50, 205, 130));
    public double CellWidth { get; set; } = 3;
    public double CellHeight { get; set; } = 10;
    public double FontSize { get; set; } = 10;
    public FontFamily FontFamily { get; set; } = new FontFamily("Consolas");

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
            var text = new FormattedText(BrailleGraphMath.ToBrailleChar(mask).ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, AreaBrush);
            context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
        }

        foreach (var (cellX, cellY, mask) in BrailleGraphMath.ComputeBrailleCells(
                     points, plotRect.Width, plotRect.Height, CellWidth, CellHeight))
        {
            var text = new FormattedText(BrailleGraphMath.ToBrailleChar(mask).ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, CurveBrush);
            context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
        }
    }
}
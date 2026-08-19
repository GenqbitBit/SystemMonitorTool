using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;
/// <summary>
/// btop-style smooth dot curve using Unicode braille packing (see
/// BrailleGraphMath) instead of one glyph per sample point — much higher
/// effective resolution than DotGraphRenderer for the same character
/// density.
/// </summary>
public class BrailleGraphRenderer : IGraphContentRenderer
{
    public IBrush Brush { get; set; } = Brushes.LimeGreen;
    public double CellWidth { get; set; } = 6;
    public double CellHeight { get; set; } = 12;
    public double FontSize { get; set; } = 10;
    public FontFamily FontFamily { get; set; } = AppFonts.Monospace;
    private readonly Dictionary<char, FormattedText> _textCache = new();
    private FontFamily? _cachedFontFamily;
    private double _cachedFontSize;

    public void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true, bool toRight = true)
    {
        var points = MetricGraphMath.ComputePoints(history, plotRect.Width, plotRect.Height, windowStart, windowEnd, minValue, maxValue, useFrozenValues);
        if (points.Count == 0)
            return;

        var typeface = new Typeface(FontFamily);
        if (!Equals(_cachedFontFamily, FontFamily) || _cachedFontSize != FontSize)
        {
            _textCache.Clear();
            _cachedFontFamily = FontFamily;
            _cachedFontSize = FontSize;
        }

        foreach (var (cellX, cellY, mask) in BrailleGraphMath.ComputeBrailleCells(
                    points, plotRect.Width, plotRect.Height, CellWidth, CellHeight))
        {
            var ch = BrailleGraphMath.ToBrailleChar(mask);
            if (!_textCache.TryGetValue(ch, out var text))
            {
                text = new FormattedText(ch.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, FontSize, Brush);
                _textCache[ch] = text;
            }
            else
            {
                text.SetForegroundBrush(Brush);
            }
            context.DrawText(text, new Point(plotRect.X + cellX, plotRect.Y + cellY));
        }
    }
}
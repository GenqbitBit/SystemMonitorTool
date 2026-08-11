using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Draws the plotted content (the curve itself) inside an already-laid-out
/// plot rect. MetricGraphView owns everything else — background, grid,
/// axis labels, plot border, current-value indicator — and stays the same
/// regardless of which renderer is active. Implementations receive raw
/// history plus the already-resolved value range so each one computes its
/// own point mapping via MetricGraphMath, rather than depending on a
/// specific upstream computation shape.
/// </summary>
public interface IGraphContentRenderer
{
    void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue);
}
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public interface IGraphContentRenderer
{
    void Draw(DrawingContext context, Rect plotRect, IReadOnlyList<MetricHistoryPoint> history,
        double minValue, double maxValue, DateTime windowStart, DateTime windowEnd,
        bool baselineAtTop = false, bool useFrozenValues = true);

    // When true, MetricGraphView skips its own dot+value-label overlay for
    // this renderer. Defaults to false so BlockAreaGraphRenderer and
    // BrailleAreaGraphRenderer need no changes; renderers that draw their own
    // current-position marker (like the percentage bars) override it to true.
    bool SuppressDefaultCurrentValueMarker => false;
}
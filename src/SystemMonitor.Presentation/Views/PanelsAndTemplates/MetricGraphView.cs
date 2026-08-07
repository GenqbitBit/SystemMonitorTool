using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class MetricGraphView : Control
{
    public static readonly StyledProperty<ObservableCollection<MetricReading>?> MetricsProperty =
        AvaloniaProperty.Register<MetricGraphView, ObservableCollection<MetricReading>?>(nameof(Metrics));

    public static readonly StyledProperty<IMetricHistoryStore?> HistoryStoreProperty =
        AvaloniaProperty.Register<MetricGraphView, IMetricHistoryStore?>(nameof(HistoryStore));

    public static readonly StyledProperty<string?> MetricIdProperty =
        AvaloniaProperty.Register<MetricGraphView, string?>(nameof(MetricId));

    public ObservableCollection<MetricReading>? Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public IMetricHistoryStore? HistoryStore
    {
        get => GetValue(HistoryStoreProperty);
        set => SetValue(HistoryStoreProperty, value);
    }

    public string? MetricId
    {
        get => GetValue(MetricIdProperty);
        set => SetValue(MetricIdProperty, value);
    }

    static MetricGraphView()
    {
        AffectsRender<MetricGraphView>(MetricsProperty, HistoryStoreProperty, MetricIdProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsNaN(Width) ? availableSize.Width : Width;
        var height = double.IsNaN(Height) ? availableSize.Height : Height;
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(Brushes.Black, new Rect(bounds.Size));

        if (HistoryStore is null || string.IsNullOrEmpty(MetricId))
            return;

        var history = HistoryStore.GetHistory(MetricId);
        if (history.Count < 2)
            return;

        var minValue = history.Min(p => p.Value);
        var maxValue = history.Max(p => p.Value);
        if (Math.Abs(maxValue - minValue) < 0.0001)
        {
            minValue -= 1;
            maxValue += 1;
        }

        var minTime = history[0].Timestamp;
        var maxTime = history[^1].Timestamp;
        var timeSpanSeconds = (maxTime - minTime).TotalSeconds;
        if (timeSpanSeconds <= 0) timeSpanSeconds = 1;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < history.Count; i++)
            {
                var point = history[i];
                var x = (point.Timestamp - minTime).TotalSeconds / timeSpanSeconds * bounds.Width;
                var normalized = (point.Value - minValue) / (maxValue - minValue);
                var y = bounds.Height - (normalized * bounds.Height);

                if (i == 0)
                    ctx.BeginFigure(new Point(x, y), isFilled: false);
                else
                    ctx.LineTo(new Point(x, y));
            }
        }

        context.DrawGeometry(null, new Pen(Brushes.LimeGreen, 1.5), geometry);
    }
}
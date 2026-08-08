using System.Collections.ObjectModel;
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

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(
            nameof(LineBrush),
            Brushes.LimeGreen);

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<MetricGraphView, double>(
            nameof(LineThickness),
            1.5);

    public static readonly StyledProperty<IBrush> GraphBackgroundProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(
            nameof(GraphBackground),
            Brushes.Black);

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

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    public IBrush GraphBackground
    {
        get => GetValue(GraphBackgroundProperty);
        set => SetValue(GraphBackgroundProperty, value);
    }

    static MetricGraphView()
    {
        AffectsRender<MetricGraphView>(
            MetricsProperty,
            HistoryStoreProperty,
            MetricIdProperty,
            LineBrushProperty,
            LineThicknessProperty,
            GraphBackgroundProperty);
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
        context.FillRectangle(GraphBackground, new Rect(bounds.Size));

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (HistoryStore is null || string.IsNullOrEmpty(MetricId))
            return;

        var history = HistoryStore.GetHistory(MetricId);
        var points = MetricGraphMath.ComputePoints(history, bounds.Width, bounds.Height);

        if (points.Count == 0)
            return;

        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: false);

            for (int i = 1; i < points.Count; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y));
            }
        }

        context.DrawGeometry(null, new Pen(LineBrush, LineThickness), geometry);
    }
}
using System.Globalization;
using System.Linq;
using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;


namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;


public class MetricGraphView : Control
{

    public static readonly StyledProperty<IBrush> GridBrushProperty =
    AvaloniaProperty.Register<MetricGraphView, IBrush>(
        nameof(GridBrush),
        new SolidColorBrush(Color.FromRgb(40, 40, 40)));

    public static readonly StyledProperty<IBrush> AxisBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(
            nameof(AxisBrush),
            Brushes.Gray);

    public static readonly StyledProperty<FontFamily> AxisFontFamilyProperty =
        AvaloniaProperty.Register<MetricGraphView, FontFamily>(
            nameof(AxisFontFamily),
            new FontFamily("Consolas"));

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

    public static readonly StyledProperty<double?> FixedMinValueProperty =
    AvaloniaProperty.Register<MetricGraphView, double?>(nameof(FixedMinValue));

    public static readonly StyledProperty<double?> FixedMaxValueProperty =
    AvaloniaProperty.Register<MetricGraphView, double?>(nameof(FixedMaxValue));

    public double? FixedMinValue
    {
        get => GetValue(FixedMinValueProperty);
        set => SetValue(FixedMinValueProperty, value);
    }

    public double? FixedMaxValue
    {
        get => GetValue(FixedMaxValueProperty);
        set => SetValue(FixedMaxValueProperty, value);
    }

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

    public IBrush GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush AxisBrush
    {
        get => GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    public FontFamily AxisFontFamily
    {
        get => GetValue(AxisFontFamilyProperty);
        set => SetValue(AxisFontFamilyProperty, value);
    }

    static MetricGraphView()
    {
        AffectsRender<MetricGraphView>(
            MetricsProperty,
            HistoryStoreProperty,
            MetricIdProperty,
            LineBrushProperty,
            LineThicknessProperty,
            GraphBackgroundProperty,
            FixedMinValueProperty,
            FixedMaxValueProperty,
            GridBrushProperty,
            AxisBrushProperty,
            AxisFontFamilyProperty);
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

        const double fontSize = 10;
        const double leftMargin = 34;
        const double bottomMargin = 16;
        const double topMargin = 12;
        const double rightMargin = 6;

        var plotWidth = Math.Max(0, bounds.Width - leftMargin - rightMargin);
        var plotHeight = Math.Max(0, bounds.Height - topMargin - bottomMargin);
        var plotOrigin = new Point(leftMargin, topMargin);

        if (plotWidth <= 0 || plotHeight <= 0)
            return;

        var typeface = new Typeface(AxisFontFamily);
        var (minValue, maxValue) = MetricGraphMath.GetValueRange(history, FixedMinValue, FixedMaxValue);
        var unitSuffix = GetUnitSuffix();

        // fewer ticks on small graphs so labels don't overlap
        var valueTickCount = plotHeight < 80 ? 3 : 5;
        var timeTickCount = plotWidth < 120 ? 3 : 4;

        // --- Y axis: grid lines + value labels ---
        foreach (var (value, normalized) in MetricGraphMath.ComputeValueAxisTicks(minValue, maxValue, valueTickCount))
        {
            var y = plotOrigin.Y + (plotHeight - normalized * plotHeight);

            context.DrawLine(new Pen(GridBrush, 1),
                new Point(plotOrigin.X, y), new Point(plotOrigin.X + plotWidth, y));

            var label = MetricGraphMath.FormatAxisValue(value) + unitSuffix;
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, fontSize, AxisBrush);
            context.DrawText(text, new Point(2, Math.Clamp(y - text.Height / 2, 0, bounds.Height - text.Height)));
        }

        // --- X axis: grid lines + relative-time labels ---
        if (history.Count >= 2)
        {
            foreach (var (normalizedX, label) in MetricGraphMath.ComputeTimeAxisTicks(
                        history[0].Timestamp, history[^1].Timestamp, timeTickCount))
            {
                var x = plotOrigin.X + normalizedX * plotWidth;

                context.DrawLine(new Pen(GridBrush, 1),
                    new Point(x, plotOrigin.Y), new Point(x, plotOrigin.Y + plotHeight));

                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, fontSize, AxisBrush);
                var textX = Math.Clamp(x - text.Width / 2, plotOrigin.X, bounds.Width - text.Width);
                context.DrawText(text, new Point(textX, plotOrigin.Y + plotHeight + 2));
            }
        }

        // --- plot area border (visually separates line/grid from the rest of the panel) ---
        context.DrawRectangle(new Pen(AxisBrush, 1), new Rect(plotOrigin, new Size(plotWidth, plotHeight)));

        // --- the line itself, inset into the plot rect ---
        var points = MetricGraphMath.ComputePoints(history, plotWidth, plotHeight, FixedMinValue, FixedMaxValue);
        if (points.Count == 0)
            return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(plotOrigin.X + points[0].X, plotOrigin.Y + points[0].Y), isFilled: false);
            for (int i = 1; i < points.Count; i++)
                ctx.LineTo(new Point(plotOrigin.X + points[i].X, plotOrigin.Y + points[i].Y));
        }
        context.DrawGeometry(null, new Pen(LineBrush, LineThickness), geometry);

        // --- current value: dot on the line + label ---
        var last = points[^1];
        var lastPoint = new Point(plotOrigin.X + last.X, plotOrigin.Y + last.Y);
        context.DrawEllipse(LineBrush, null, lastPoint, 2.5, 2.5);

        var currentLabel = MetricGraphMath.FormatAxisValue(history[^1].Value) + unitSuffix;
        var currentText = new FormattedText(currentLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, fontSize, LineBrush);
        var labelX = Math.Clamp(lastPoint.X + 4, plotOrigin.X, bounds.Width - currentText.Width - 2);
        var labelY = Math.Clamp(lastPoint.Y - currentText.Height - 2, 0, bounds.Height - currentText.Height);
        context.DrawText(currentText, new Point(labelX, labelY));
    }

    private string GetUnitSuffix()
    {
        var metric = Metrics?.FirstOrDefault(m => m.Id == MetricId);
        return string.IsNullOrEmpty(metric?.Unit) ? string.Empty : metric!.Unit;
    }
}
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public class MetricGraphView : Control
{
    public static readonly StyledProperty<IGraphContentRenderer?> ContentRendererProperty =
        AvaloniaProperty.Register<MetricGraphView, IGraphContentRenderer?>(nameof(ContentRenderer));

    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(
            nameof(GridBrush), new SolidColorBrush(Color.FromRgb(40, 40, 40)));

    public static readonly StyledProperty<IBrush> AxisBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(nameof(AxisBrush), Brushes.Gray);

    public static readonly StyledProperty<FontFamily> AxisFontFamilyProperty =
    AvaloniaProperty.Register<MetricGraphView, FontFamily>(
        nameof(AxisFontFamily),
        AppFonts.Monospace);

    public static readonly StyledProperty<ObservableCollection<MetricReading>?> MetricsProperty =
        AvaloniaProperty.Register<MetricGraphView, ObservableCollection<MetricReading>?>(nameof(Metrics));

    public static readonly StyledProperty<IMetricHistoryStore?> HistoryStoreProperty =
        AvaloniaProperty.Register<MetricGraphView, IMetricHistoryStore?>(nameof(HistoryStore));

    public static readonly StyledProperty<string?> MetricIdProperty =
        AvaloniaProperty.Register<MetricGraphView, string?>(nameof(MetricId));

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(nameof(LineBrush), Brushes.LimeGreen);

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<MetricGraphView, double>(nameof(LineThickness), 1.5);

    public static readonly StyledProperty<IBrush> GraphBackgroundProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(nameof(GraphBackground), Brushes.Black);

    public static readonly StyledProperty<double?> FixedMinValueProperty =
        AvaloniaProperty.Register<MetricGraphView, double?>(nameof(FixedMinValue));

    public static readonly StyledProperty<double?> FixedMaxValueProperty =
        AvaloniaProperty.Register<MetricGraphView, double?>(nameof(FixedMaxValue));

    public static readonly StyledProperty<string?> SecondaryMetricIdProperty =
        AvaloniaProperty.Register<MetricGraphView, string?>(nameof(SecondaryMetricId));

    public static readonly StyledProperty<IGraphContentRenderer?> SecondaryContentRendererProperty =
        AvaloniaProperty.Register<MetricGraphView, IGraphContentRenderer?>(nameof(SecondaryContentRenderer));

    public static readonly StyledProperty<IBrush> SecondaryLineBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(nameof(SecondaryLineBrush), Brushes.Magenta);

    public static readonly StyledProperty<IBrush> BaselineBrushProperty =
        AvaloniaProperty.Register<MetricGraphView, IBrush>(nameof(BaselineBrush), Brushes.Gray);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<MetricGraphView, bool>(nameof(ShowGrid), defaultValue: false);

    public static readonly StyledProperty<bool> ToRightProperty =
        AvaloniaProperty.Register<MetricGraphView, bool>(nameof(ToRight), defaultValue: true);

    // Independent scan direction for the secondary (bottom) half of a
    // mirrored graph. Defaults to true (same as ToRight's default) so
    // existing mirrored XAML that never set this keeps its current
    // behavior. Only meaningful when SecondaryMetricId is set.
    public static readonly StyledProperty<bool> SecondaryToRightProperty =
        AvaloniaProperty.Register<MetricGraphView, bool>(nameof(SecondaryToRight), defaultValue: true);

    public static readonly StyledProperty<Theming.GraphRole?> GraphRoleProperty =
        AvaloniaProperty.Register<MetricGraphView, Theming.GraphRole?>(nameof(GraphRole));

    private INotifyCollectionChanged? _subscribedCollection;

    public Theming.GraphRole? GraphRole
    {
        get => GetValue(GraphRoleProperty);
        set => SetValue(GraphRoleProperty, value);
    }

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

    public IGraphContentRenderer? ContentRenderer
    {
        get => GetValue(ContentRendererProperty);
        set => SetValue(ContentRendererProperty, value);
    }

    public string? SecondaryMetricId
    {
        get => GetValue(SecondaryMetricIdProperty);
        set => SetValue(SecondaryMetricIdProperty, value);
    }

    public IGraphContentRenderer? SecondaryContentRenderer
    {
        get => GetValue(SecondaryContentRendererProperty);
        set => SetValue(SecondaryContentRendererProperty, value);
    }

    public IBrush SecondaryLineBrush
    {
        get => GetValue(SecondaryLineBrushProperty);
        set => SetValue(SecondaryLineBrushProperty, value);
    }

    public IBrush BaselineBrush
    {
        get => GetValue(BaselineBrushProperty);
        set => SetValue(BaselineBrushProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool ToRight
    {
        get => GetValue(ToRightProperty);
        set => SetValue(ToRightProperty, value);
    }

    public bool SecondaryToRight
    {
        get => GetValue(SecondaryToRightProperty);
        set => SetValue(SecondaryToRightProperty, value);
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
            AxisFontFamilyProperty,
            SecondaryMetricIdProperty,
            SecondaryContentRendererProperty,
            SecondaryLineBrushProperty,
            BaselineBrushProperty,
            ShowGridProperty,
            ToRightProperty,
            SecondaryToRightProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsNaN(Width)
            ? (double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width)
            : Width;

        var height = double.IsNaN(Height)
            ? (double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height)
            : Height;

        return new Size(width, height);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MetricsProperty)
        {
            UnsubscribeFromMetricsCollection();
            SubscribeToMetricsCollection();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeToMetricsCollection();

        ApplyThemeColors();
        Theming.ThemeRuntime.Service.ThemeChanged += OnThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeFromMetricsCollection();
        Theming.ThemeRuntime.Service.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, Domain.Models.Theming.ThemeDefinition theme)
    {
        ApplyThemeColors();
        InvalidateVisual();
    }

    private void ApplyThemeColors()
    {
        if (GraphRole is not { } role)
            return;

        var (primary, secondary) = Theming.GraphRoleMap.Resolve(role, Theming.ThemeRuntime.Service.CurrentTheme);

        if (ContentRenderer is IThemeableGraphRenderer themeable)
            themeable.ApplyPalette(primary, secondary);

        if (SecondaryContentRenderer is IThemeableGraphRenderer secondaryThemeable)
            secondaryThemeable.ApplyPalette(primary, secondary);
    }

    private void SubscribeToMetricsCollection()
    {
        if (_subscribedCollection != null || Metrics is not INotifyCollectionChanged incc)
            return;

        incc.CollectionChanged += OnMetricsCollectionChanged;
        _subscribedCollection = incc;
    }

    private void UnsubscribeFromMetricsCollection()
    {
        if (_subscribedCollection is null)
            return;

        _subscribedCollection.CollectionChanged -= OnMetricsCollectionChanged;
        _subscribedCollection = null;
    }

    private void OnMetricsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
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

        // Margins only exist to reserve space for grid/axis labels. When the
        // grid is off, the plot area fills the full control bounds so the
        // border IS the graph area — no leftover reserved space.
        double leftMargin, bottomMargin, topMargin, rightMargin;
        if (ShowGrid)
        {
            leftMargin = 34;
            bottomMargin = 16;
            topMargin = 12;
            rightMargin = 6;
        }
        else
        {
            leftMargin = 0;
            bottomMargin = 0;
            topMargin = 0;
            rightMargin = 0;
        }

        var plotWidth = Math.Max(0, bounds.Width - leftMargin - rightMargin);
        var plotHeight = Math.Max(0, bounds.Height - topMargin - bottomMargin);
        var plotOrigin = new Point(leftMargin, topMargin);

        if (plotWidth <= 0 || plotHeight <= 0)
            return;

        if (!string.IsNullOrEmpty(SecondaryMetricId))
        {
            RenderMirrored(context, bounds, plotOrigin, plotWidth, plotHeight);
            return;
        }

        RenderSingle(context, bounds, plotOrigin, plotWidth, plotHeight);
    }

    private void RenderSingle(DrawingContext context, Rect bounds, Point plotOrigin, double plotWidth, double plotHeight)
    {
        var history = HistoryStore!.GetHistory(MetricId!);

        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd - HistoryStore!.Window;

        const double fontSize = 10;
        var typeface = new Typeface(AxisFontFamily);

        var useFrozenValues = !(FixedMinValue.HasValue && FixedMaxValue.HasValue);
        var (minValue, maxValue) = MetricGraphMath.ResolveDisplayRange(
            HistoryStore!.GetCommittedRange(MetricId!), FixedMinValue, FixedMaxValue);
        var unitSuffix = GetUnitSuffix(MetricId);

        var valueTickCount = plotHeight < 80 ? 3 : 5;
        var timeTickCount = plotWidth < 120 ? 3 : 4;

        if (ShowGrid)
        {
            foreach (var (value, normalized) in MetricGraphMath.ComputeValueAxisTicks(minValue, maxValue, valueTickCount))
            {
                var y = plotOrigin.Y + (plotHeight - normalized * plotHeight);
                context.DrawLine(new Pen(GridBrush, 1), new Point(plotOrigin.X, y), new Point(plotOrigin.X + plotWidth, y));

                var label = MetricGraphMath.FormatAxisValue(value) + unitSuffix;
                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, AxisBrush);
                context.DrawText(text, new Point(2, Math.Clamp(y - text.Height / 2, 0, bounds.Height - text.Height)));
            }

            foreach (var (normalizedX, label) in MetricGraphMath.ComputeTimeAxisTicks(windowStart, windowEnd, timeTickCount))
            {
                var effectiveX = ToRight ? normalizedX : 1 - normalizedX;
                var x = plotOrigin.X + effectiveX * plotWidth;
                context.DrawLine(new Pen(GridBrush, 1), new Point(x, plotOrigin.Y), new Point(x, plotOrigin.Y + plotHeight));

                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, AxisBrush);
                var textX = Math.Clamp(x - text.Width / 2, plotOrigin.X, bounds.Width - text.Width);
                context.DrawText(text, new Point(textX, plotOrigin.Y + plotHeight + 2));
            }
        }

        context.DrawRectangle(new Pen(AxisBrush, 1), new Rect(plotOrigin, new Size(plotWidth, plotHeight)));

        var plotRect = new Rect(plotOrigin, new Size(plotWidth, plotHeight));
        var activeRenderer = ContentRenderer ?? new LineGraphRenderer { LineBrush = LineBrush, LineThickness = LineThickness };

        using (context.PushClip(plotRect))
        {
            activeRenderer.Draw(context, plotRect, history, minValue, maxValue, windowStart, windowEnd, useFrozenValues: useFrozenValues, toRight: ToRight);
        }

        if (!activeRenderer.SuppressDefaultCurrentValueMarker)
        {
            var points = MetricGraphMath.ComputePoints(history, plotWidth, plotHeight, windowStart, windowEnd, minValue, maxValue, useFrozenValues, ToRight);
            if (points.Count > 0)
                DrawCurrentValueLabel(context, plotOrigin, points, history, LineBrush, unitSuffix, typeface, fontSize, bounds, ToRight);
        }
    }

    private void RenderMirrored(DrawingContext context, Rect bounds, Point plotOrigin, double plotWidth, double plotHeight)
    {
        var primaryHistory = HistoryStore!.GetHistory(MetricId!);
        var secondaryHistory = HistoryStore!.GetHistory(SecondaryMetricId!);

        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd - HistoryStore!.Window;

        const double fontSize = 10;
        const double halfGap = 1;
        var typeface = new Typeface(AxisFontFamily);

        var halfHeight = (plotHeight - halfGap * 2) / 2;
        var topRect = new Rect(plotOrigin.X, plotOrigin.Y, plotWidth, halfHeight);
        var baselineY = plotOrigin.Y + halfHeight + halfGap;
        var bottomRect = new Rect(plotOrigin.X, baselineY, plotWidth, halfHeight);

        var useFrozenValues = !(FixedMinValue.HasValue && FixedMaxValue.HasValue);

        // Each half scales against its OWN metric's committed range. When a
        // fixed range is set it's shared (one property on the whole view), so
        // the halves stay comparable. When auto-scaling, exact cross-metric
        // comparability is secondary to keeping each half's own past immutable.
        var primaryRange = MetricGraphMath.ResolveDisplayRange(
            HistoryStore!.GetCommittedRange(MetricId!), FixedMinValue, FixedMaxValue);
        var secondaryRange = MetricGraphMath.ResolveDisplayRange(
            HistoryStore!.GetCommittedRange(SecondaryMetricId!), FixedMinValue, FixedMaxValue);

        var valueTickCount = halfHeight < 80 ? 3 : 5;
        var timeTickCount = plotWidth < 120 ? 3 : 4;

        if (ShowGrid)
        {
            foreach (var (_, normalized) in MetricGraphMath.ComputeValueAxisTicks(primaryRange.Min, primaryRange.Max, valueTickCount))
            {
                var yTop = topRect.Y + (topRect.Height - normalized * topRect.Height);
                context.DrawLine(new Pen(GridBrush, 1), new Point(topRect.X, yTop), new Point(topRect.X + topRect.Width, yTop));
            }

            foreach (var (_, normalized) in MetricGraphMath.ComputeValueAxisTicks(secondaryRange.Min, secondaryRange.Max, valueTickCount))
            {
                var yBottom = bottomRect.Y + normalized * bottomRect.Height;
                context.DrawLine(new Pen(GridBrush, 1), new Point(bottomRect.X, yBottom), new Point(bottomRect.X + bottomRect.Width, yBottom));
            }

            // Time axis is drawn independently per half rather than once for
            // the whole plot, since the top and bottom can now scan in
            // opposite directions (ToRight / SecondaryToRight) — a single
            // shared set of tick positions would only be time-correct for
            // whichever half's direction it was computed against.
            foreach (var (normalizedX, _) in MetricGraphMath.ComputeTimeAxisTicks(windowStart, windowEnd, timeTickCount))
            {
                var effectiveX = ToRight ? normalizedX : 1 - normalizedX;
                var x = plotOrigin.X + effectiveX * plotWidth;
                context.DrawLine(new Pen(GridBrush, 1), new Point(x, topRect.Y), new Point(x, topRect.Y + topRect.Height));
            }

            foreach (var (normalizedX, label) in MetricGraphMath.ComputeTimeAxisTicks(windowStart, windowEnd, timeTickCount))
            {
                var effectiveX = ToRight ? normalizedX : 1 - normalizedX;
                var x = plotOrigin.X + effectiveX * plotWidth;

                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, AxisBrush);
                var textX = Math.Clamp(x - text.Width / 2, plotOrigin.X, bounds.Width - text.Width);
                // Labeled just above the top half, so it doesn't collide
                // with the shared baseline sitting between the two halves.
                var textY = Math.Clamp(topRect.Y - text.Height - 1, 0, bounds.Height - text.Height);
                context.DrawText(text, new Point(textX, textY));
            }

            foreach (var (normalizedX, _) in MetricGraphMath.ComputeTimeAxisTicks(windowStart, windowEnd, timeTickCount))
            {
                var effectiveX = SecondaryToRight ? normalizedX : 1 - normalizedX;
                var x = plotOrigin.X + effectiveX * plotWidth;
                context.DrawLine(new Pen(GridBrush, 1), new Point(x, bottomRect.Y), new Point(x, bottomRect.Y + bottomRect.Height));
            }

            foreach (var (normalizedX, label) in MetricGraphMath.ComputeTimeAxisTicks(windowStart, windowEnd, timeTickCount))
            {
                var effectiveX = SecondaryToRight ? normalizedX : 1 - normalizedX;
                var x = plotOrigin.X + effectiveX * plotWidth;

                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, AxisBrush);
                var textX = Math.Clamp(x - text.Width / 2, plotOrigin.X, bounds.Width - text.Width);
                // Labeled just below the bottom half — this is the original
                // single-time-axis label position, kept here since bottom
                // uses the same default direction (SecondaryToRight=true)
                // as before this feature existed.
                var textY = plotOrigin.Y + plotHeight + 2;
                context.DrawText(text, new Point(textX, textY));
            }
        }

        context.DrawRectangle(new Pen(AxisBrush, 1), topRect);
        context.DrawRectangle(new Pen(AxisBrush, 1), bottomRect);

        var primaryRenderer = ContentRenderer ?? new LineGraphRenderer { LineBrush = LineBrush, LineThickness = LineThickness };
        var secondaryRenderer = SecondaryContentRenderer ?? primaryRenderer;

        using (context.PushClip(topRect))
        {
            primaryRenderer.Draw(context, topRect, primaryHistory, primaryRange.Min, primaryRange.Max, windowStart, windowEnd, useFrozenValues: useFrozenValues, toRight: ToRight);
        }

        using (context.PushClip(bottomRect))
        {
            secondaryRenderer.Draw(context, bottomRect, secondaryHistory, secondaryRange.Max, secondaryRange.Min, windowStart, windowEnd, baselineAtTop: true, useFrozenValues: useFrozenValues, toRight: SecondaryToRight);
        }

        context.DrawLine(new Pen(BaselineBrush, 1.5), new Point(plotOrigin.X, baselineY - halfGap / 2), new Point(plotOrigin.X + plotWidth, baselineY - halfGap / 2));

        var primaryUnit = GetUnitSuffix(MetricId);
        var secondaryUnit = GetUnitSuffix(SecondaryMetricId);

        if (!primaryRenderer.SuppressDefaultCurrentValueMarker)
            {
                var primaryPoints = MetricGraphMath.ComputePoints(primaryHistory, topRect.Width, topRect.Height, windowStart, windowEnd, primaryRange.Min, primaryRange.Max, useFrozenValues, ToRight);
                if (primaryPoints.Count > 0)
                    DrawCurrentValueLabel(context, topRect.Position, primaryPoints, primaryHistory, LineBrush, primaryUnit, typeface, fontSize, bounds, ToRight, labelYOffset: -4);

                if (!secondaryRenderer.SuppressDefaultCurrentValueMarker)
                {
                    var secondaryPoints = MetricGraphMath.ComputePoints(secondaryHistory, bottomRect.Width, bottomRect.Height, windowStart, windowEnd, secondaryRange.Max, secondaryRange.Min, useFrozenValues, SecondaryToRight);
                    if (secondaryPoints.Count > 0)
                        DrawCurrentValueLabel(context, bottomRect.Position, secondaryPoints, secondaryHistory, SecondaryLineBrush, secondaryUnit, typeface, fontSize, bounds, SecondaryToRight, labelYOffset: 25);
                }
            }
    }
    
    private static void DrawCurrentValueLabel(
    DrawingContext context, Point rectOrigin, IReadOnlyList<(double X, double Y)> points,
    IReadOnlyList<MetricHistoryPoint> history, IBrush brush, string unitSuffix,
    Typeface typeface, double fontSize, Rect bounds, bool toRight, double labelYOffset = 0)
    {
        // points is ascending-by-X but its endpoint mapping to "newest" flips
        // with toRight (see MetricGraphMath.ComputePoints): points[^1] is newest
        // when toRight, points[0] is newest when !toRight. history is always
        // chronological, so history[^1] is always the newest value.
        var current = toRight ? points[^1] : points[0];
        var currentPoint = new Point(rectOrigin.X + current.X, rectOrigin.Y + current.Y);
        context.DrawEllipse(brush, null, currentPoint, 2.5, 2.5);

        var currentLabel = MetricGraphMath.FormatAxisValue(history[^1].Value) + unitSuffix;
        var currentText = new FormattedText(currentLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        var labelX = Math.Clamp(currentPoint.X + 4, 0, bounds.Width - currentText.Width - 2);
        var labelY = Math.Clamp(currentPoint.Y - currentText.Height - 2 + labelYOffset, 0, bounds.Height - currentText.Height);
        context.DrawText(currentText, new Point(labelX, labelY));
    }

    private string GetUnitSuffix(string? metricId)
    {
        var metric = Metrics?.FirstOrDefault(m => m.Id == metricId);
        return string.IsNullOrEmpty(metric?.Unit) ? string.Empty : metric!.Unit;
    }
}
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Renders a set of metrics as a header + list.
/// Selection (which data shows) — set ONE of:
///   - CategoryLabel="CPU"  -> every reading in that category
///   - MetricId="cpu.usage" -> exactly one specific reading (wins if both are set)
/// CategoryLabel also drives the header text even when MetricId is used for selection.
/// Presentation (how each row looks) — independent of selection and each other:
///   - ShowLabel (default true)         -> show/hide each reading's Label text
///   - ShowValue (default true)         -> show/hide each reading's DisplayValue text
///   - ShowCategoryHeader (default true) -> show/hide the header TextBlock entirely
/// </summary>
public partial class CategoryMetricsView : UserControl
{
    public static readonly StyledProperty<IEnumerable<MetricReading>?> MetricsProperty =
        AvaloniaProperty.Register<CategoryMetricsView, IEnumerable<MetricReading>?>(nameof(Metrics));

    public static readonly StyledProperty<string?> CategoryLabelProperty =
        AvaloniaProperty.Register<CategoryMetricsView, string?>(nameof(CategoryLabel));

    public static readonly StyledProperty<string?> MetricIdProperty =
        AvaloniaProperty.Register<CategoryMetricsView, string?>(nameof(MetricId));

    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<CategoryMetricsView, bool>(nameof(ShowLabel), defaultValue: true);

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<CategoryMetricsView, bool>(nameof(ShowValue), defaultValue: true);

    public static readonly StyledProperty<bool> ShowCategoryHeaderProperty =
        AvaloniaProperty.Register<CategoryMetricsView, bool>(nameof(ShowCategoryHeader), defaultValue: true);

    public static readonly DirectProperty<CategoryMetricsView, IEnumerable<MetricReading>?> FilteredMetricsProperty =
        AvaloniaProperty.RegisterDirect<CategoryMetricsView, IEnumerable<MetricReading>?>(
            nameof(FilteredMetrics), o => o.FilteredMetrics);

    public CategoryMetricsView()
    {
        InitializeComponent();
    }

    public IEnumerable<MetricReading>? Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public string? CategoryLabel
    {
        get => GetValue(CategoryLabelProperty);
        set => SetValue(CategoryLabelProperty, value);
    }

    public string? MetricId
    {
        get => GetValue(MetricIdProperty);
        set => SetValue(MetricIdProperty, value);
    }

    public bool ShowLabel
    {
        get => GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    public bool ShowCategoryHeader
    {
        get => GetValue(ShowCategoryHeaderProperty);
        set => SetValue(ShowCategoryHeaderProperty, value);
    }

    public IEnumerable<MetricReading>? FilteredMetrics =>
        Metrics?.Where(m =>
            MetricId != null
                ? m.Id == MetricId
                : string.Equals(m.Category, CategoryLabel, System.StringComparison.OrdinalIgnoreCase));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MetricsProperty
            || change.Property == CategoryLabelProperty
            || change.Property == MetricIdProperty)
        {
            RaisePropertyChanged(FilteredMetricsProperty, default, default);
        }
    }
}
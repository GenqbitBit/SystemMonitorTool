using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// A bound value of null still counts as a "local value" in Avalonia and
/// therefore outranks Style/theme setters (e.g. an app-wide
/// Style Selector="TextBlock" setting FontFamily). For properties on this view
/// where null is meant to mean "don't override — let the app Style/theme
/// decide" (ContentFontFamily, LabelForeground), bind through this converter
/// so a null source value clears the local value entirely instead of forcing
/// it to null, letting Style setters apply as expected.
/// </summary>
internal sealed class NullToUnsetConverter : IValueConverter
{
    public static readonly NullToUnsetConverter Instance = new();

    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => value ?? AvaloniaProperty.UnsetValue;

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}

/// <summary>
/// Renders a set of metrics as a header + list.
/// Selection (which data shows) — set ONE of:
///   - CategoryLabel="CPU"  -> every reading in that category
///   - MetricId="cpu.usage" -> exactly one specific reading (wins if both are set)
/// PrimaryOnly="True" additionally restricts a CategoryLabel selection to just
/// each device's primary/core reading (e.g. GPU core temp, not Hot Spot/Memory
/// Junction/etc.) — ignored when MetricId is set, since that already picks one row.
/// GpuDeviceId additionally restricts a CategoryLabel selection to readings from
/// one physical GPU (matched on MetricReading.GpuDeviceId) — same "ignored when
/// MetricId is set" rule as PrimaryTempOnly, and composes with it (e.g. GPU +
/// GpuDeviceId + PrimaryTempOnly = that device's primary reading only).
/// CategoryLabel also drives the header text even when MetricId is used for selection.
/// Presentation (how each row looks) — independent of selection and each other:
///   - ShowLabel (default true)         -> show/hide each reading's Label text
///   - ShowValue (default true)         -> show/hide each reading's DisplayValue text
///   - ShowCategoryHeader (default true) -> show/hide the header TextBlock entirely
/// Styling — all optional, each falls back to the previous hardcoded look so
/// existing usages render unchanged; set any of these per-instance to override:
///   - CategoryFontSize / CategoryOpacity   -> header text
///   - MetricFontSize                       -> both label and value text
///   - LabelFontWeight / LabelForeground    -> label text
///   - ValueFontWeight / ValueForeground    -> value text (Foreground falls back
///                                              to MetricValueBrush resource if unset)
///   - ContentFontFamily                    -> shared font family for header + rows
///   - ContentMaxWidth / ContentSpacing /
///     ContentHorizontalAlignment           -> outer layout
///
/// Metrics refresh: the bound ObservableCollection is now mutated in place every
/// tick (see ObservableCollectionSyncExtensions.SyncFrom) rather than replaced, so
/// this view can't rely on the Metrics property itself changing. It subscribes to
/// INotifyCollectionChanged on whichever collection Metrics currently points to,
/// re-subscribing whenever that property is reassigned to a different instance,
/// and forces FilteredMetrics to re-evaluate on every collection mutation.
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

    // When true, only readings with IsPrimary == true are shown (e.g. each
    // GPU's core temp, not its Hot Spot/Memory Junction/etc. sub-readings).
    // Has no effect when MetricId selects a single specific reading.
    public static readonly StyledProperty<bool> PrimaryTempOnlyProperty =
        AvaloniaProperty.Register<CategoryMetricsView, bool>(nameof(PrimaryTempOnly), defaultValue: false);

    // When set, restricts a CategoryLabel selection to readings from one
    // physical GPU (MetricReading.GpuDeviceId). This is the actual per-device
    // association — never derive device identity from Index or by parsing
    // Label text. Has no effect when MetricId selects a single specific reading.
    public static readonly StyledProperty<string?> GpuDeviceIdProperty =
        AvaloniaProperty.Register<CategoryMetricsView, string?>(nameof(GpuDeviceId));

    public static readonly DirectProperty<CategoryMetricsView, IEnumerable<MetricReading>?> FilteredMetricsProperty =
        AvaloniaProperty.RegisterDirect<CategoryMetricsView, IEnumerable<MetricReading>?>(
            nameof(FilteredMetrics), o => o.FilteredMetrics);

    // --- Styling properties -------------------------------------------------
    // Each defaults to the value that used to be hardcoded in the AXAML, so any
    // existing consumer that doesn't set these renders exactly as before. Panels
    // that want a different look (bigger header, non-bold label, custom brush,
    // tighter layout, etc.) now set these instead of touching the template.

    public static readonly StyledProperty<double> CategoryFontSizeProperty =
        AvaloniaProperty.Register<CategoryMetricsView, double>(nameof(CategoryFontSize), defaultValue: 11);

    public static readonly StyledProperty<double> CategoryOpacityProperty =
        AvaloniaProperty.Register<CategoryMetricsView, double>(nameof(CategoryOpacity), defaultValue: 0.6);

    public static readonly StyledProperty<double> MetricFontSizeProperty =
        AvaloniaProperty.Register<CategoryMetricsView, double>(nameof(MetricFontSize), defaultValue: 11);

    public static readonly StyledProperty<FontWeight> LabelFontWeightProperty =
        AvaloniaProperty.Register<CategoryMetricsView, FontWeight>(nameof(LabelFontWeight), defaultValue: FontWeight.SemiBold);

    // Value never had an explicit weight in the original template (so it rendered
    // at whatever the default/Normal weight is) — default kept the same here.
    public static readonly StyledProperty<FontWeight> ValueFontWeightProperty =
        AvaloniaProperty.Register<CategoryMetricsView, FontWeight>(nameof(ValueFontWeight), defaultValue: FontWeight.Normal);

    // Null means "inherit ambient font family" (same as the original template,
    // which never set FontFamily anywhere). Applies to header, label, and value —
    // set it once per instance rather than per-row. Bound in XAML through
    // NullToUnsetConverter so an unset instance truly defers to app Styles.
    public static readonly StyledProperty<FontFamily?> ContentFontFamilyProperty =
        AvaloniaProperty.Register<CategoryMetricsView, FontFamily?>(nameof(ContentFontFamily));

    // Null means "don't set it" -> label inherits ambient foreground, same as
    // the original hardcoded template (which never set a label Foreground).
    // Bound in XAML through NullToUnsetConverter so an unset instance truly
    // defers to app Styles/theme instead of forcing Foreground to null.
    public static readonly StyledProperty<IBrush?> LabelForegroundProperty =
        AvaloniaProperty.Register<CategoryMetricsView, IBrush?>(nameof(LabelForeground));

    // Null means "use the MetricValueBrush theme resource", matching the
    // original hardcoded DynamicResource binding. Set this to override per-instance.
    public static readonly StyledProperty<IBrush?> ValueForegroundProperty =
        AvaloniaProperty.Register<CategoryMetricsView, IBrush?>(nameof(ValueForeground));

    public static readonly StyledProperty<double> ContentMaxWidthProperty =
        AvaloniaProperty.Register<CategoryMetricsView, double>(nameof(ContentMaxWidth), defaultValue: 300);

    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<CategoryMetricsView, double>(nameof(ContentSpacing), defaultValue: 2);

    public static readonly StyledProperty<HorizontalAlignment> ContentHorizontalAlignmentProperty =
        AvaloniaProperty.Register<CategoryMetricsView, HorizontalAlignment>(
            nameof(ContentHorizontalAlignment), defaultValue: HorizontalAlignment.Center);

    // Tracks whichever collection instance we're currently subscribed to, so we can
    // unsubscribe cleanly when Metrics is replaced or the view is detached — this
    // prevents duplicate handlers piling up and keeps a detached view from being
    // pinned alive by an old collection's event reference.
    private INotifyCollectionChanged? _subscribedCollection;

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

    public bool PrimaryTempOnly
    {
        get => GetValue(PrimaryTempOnlyProperty);
        set => SetValue(PrimaryTempOnlyProperty, value);
    }

    public string? GpuDeviceId
    {
        get => GetValue(GpuDeviceIdProperty);
        set => SetValue(GpuDeviceIdProperty, value);
    }

    public double CategoryFontSize
    {
        get => GetValue(CategoryFontSizeProperty);
        set => SetValue(CategoryFontSizeProperty, value);
    }

    public double CategoryOpacity
    {
        get => GetValue(CategoryOpacityProperty);
        set => SetValue(CategoryOpacityProperty, value);
    }

    public double MetricFontSize
    {
        get => GetValue(MetricFontSizeProperty);
        set => SetValue(MetricFontSizeProperty, value);
    }

    public FontWeight LabelFontWeight
    {
        get => GetValue(LabelFontWeightProperty);
        set => SetValue(LabelFontWeightProperty, value);
    }

    public FontWeight ValueFontWeight
    {
        get => GetValue(ValueFontWeightProperty);
        set => SetValue(ValueFontWeightProperty, value);
    }

    public FontFamily? ContentFontFamily
    {
        get => GetValue(ContentFontFamilyProperty);
        set => SetValue(ContentFontFamilyProperty, value);
    }

    public IBrush? LabelForeground
    {
        get => GetValue(LabelForegroundProperty);
        set => SetValue(LabelForegroundProperty, value);
    }

    public IBrush? ValueForeground
    {
        get => GetValue(ValueForegroundProperty);
        set => SetValue(ValueForegroundProperty, value);
    }

    public double ContentMaxWidth
    {
        get => GetValue(ContentMaxWidthProperty);
        set => SetValue(ContentMaxWidthProperty, value);
    }

    public double ContentSpacing
    {
        get => GetValue(ContentSpacingProperty);
        set => SetValue(ContentSpacingProperty, value);
    }

    public HorizontalAlignment ContentHorizontalAlignment
    {
        get => GetValue(ContentHorizontalAlignmentProperty);
        set => SetValue(ContentHorizontalAlignmentProperty, value);
    }

    public IEnumerable<MetricReading>? FilteredMetrics
    {
        get
        {
            if (MetricId != null)
            {
                return Metrics?.Where(m => m.Id == MetricId);
            }

            var byCategory = Metrics?.Where(m =>
                string.Equals(m.Category, CategoryLabel, System.StringComparison.OrdinalIgnoreCase));

            if (GpuDeviceId != null)
            {
                byCategory = byCategory?.Where(m => m.GpuDeviceId == GpuDeviceId);
            }

            return PrimaryTempOnly
                ? byCategory?.Where(m => m.IsPrimary)
                : byCategory;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MetricsProperty)
        {
            // Metrics was reassigned to a different collection instance (or null) —
            // move our CollectionChanged subscription onto whatever it points to now.
            UnsubscribeFromMetricsCollection();
            SubscribeToMetricsCollection();
        }

        if (change.Property == MetricsProperty
            || change.Property == CategoryLabelProperty
            || change.Property == MetricIdProperty
            || change.Property == PrimaryTempOnlyProperty
            || change.Property == GpuDeviceIdProperty)
        {
            RaisePropertyChanged(FilteredMetricsProperty, default, default);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Covers reuse (e.g. a recycled/re-attached control) where Metrics was set
        // while the view was detached and OnPropertyChanged's subscribe was skipped.
        SubscribeToMetricsCollection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeFromMetricsCollection();
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
        // An in-place mutation (Move/Insert/Replace/RemoveAt from SyncFrom) on the
        // same collection instance — the Metrics property itself hasn't changed, so
        // bindings won't know FilteredMetrics is stale unless we say so explicitly.
        RaisePropertyChanged(FilteredMetricsProperty, default, default);
    }
}
# CategoryMetricsView — Property Reference

`SystemMonitor.Presentation.Views.PanelsAndTemplates.CategoryMetricsView`

Renders a set of metrics as an optional header + list of label/value rows.
19 bindable properties total: 5 selection, 3 visibility, 11 styling.

## Selection — what data shows

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Metrics` | `IEnumerable<MetricReading>?` | — | Source collection (usually bound once from the parent VM) |
| `CategoryLabel` | `string?` | — | Selects all readings in a category; also drives header text even when `MetricId` is set |
| `MetricId` | `string?` | — | Selects one specific reading; wins over `CategoryLabel` for selection |
| `GpuDeviceId` | `string?` | — | Restricts a `CategoryLabel` selection to one GPU device; ignored when `MetricId` is set |
| `PrimaryTempOnly` | `bool` | `false` | Restricts a `CategoryLabel` selection to each device's primary reading only; ignored when `MetricId` is set |
| `FilteredMetrics` | `IEnumerable<MetricReading>?` (read-only) | — | Computed result of the above — what the `ItemsControl` actually binds to |

## Visibility — what's shown per row

| Property | Type | Default | Purpose |
|---|---|---|---|
| `ShowCategoryHeader` | `bool` | `true` | Show/hide the header `TextBlock` |
| `ShowLabel` | `bool` | `true` | Show/hide each row's label `TextBlock` |
| `ShowValue` | `bool` | `true` | Show/hide each row's value `TextBlock` |

## Styling — header

| Property | Type | Default |
|---|---|---|
| `CategoryFontSize` | `double` | `11` |
| `CategoryOpacity` | `double` | `0.6` |

## Styling — rows (label/value)

| Property | Type | Default |
|---|---|---|
| `MetricFontSize` | `double` | `11` (applies to both label and value) |
| `LabelFontWeight` | `FontWeight` | `SemiBold` |
| `ValueFontWeight` | `FontWeight` | `Normal` |
| `LabelForeground` | `IBrush?` | `null` → inherits ambient/app Style |
| `ValueForeground` | `IBrush?` | `null` → falls back to `MetricValueBrush` resource |
| `ContentFontFamily` | `FontFamily?` | `null` → inherits ambient/app Style (shared across header + rows) |

## Styling — outer layout

| Property | Type | Default |
|---|---|---|
| `ContentMaxWidth` | `double` | `300` |
| `ContentSpacing` | `double` | `2` |
| `ContentHorizontalAlignment` | `HorizontalAlignment` | `Center` |

## Usage examples

**Value-only tile (no label, no header):**
```xml
<panels:CategoryMetricsView MetricId="cpu.usage"
                             ShowLabel="False"
                             ShowCategoryHeader="False" />
```

**Label-only for a whole category:**
```xml
<panels:CategoryMetricsView CategoryLabel="CPU"
                             ShowValue="False" />
```

**GPU device-specific, primary temp only:**
```xml
<panels:CategoryMetricsView CategoryLabel="GPU"
                             GpuDeviceId="0"
                             PrimaryTempOnly="True"
                             ShowLabel="False" />
```

**Custom typography — bold value, regular label, monospace:**
```xml
<panels:CategoryMetricsView MetricId="cpu.usage"
                             LabelFontWeight="Normal"
                             ValueFontWeight="Bold"
                             ContentFontFamily="Consolas" />
```

**"Hero" tile — big, bold, no label/header:**
```xml
<panels:CategoryMetricsView MetricId="gpu.usage.0"
                             ShowLabel="False"
                             ShowCategoryHeader="False"
                             ContentFontFamily="Segoe UI"
                             ValueFontWeight="Bold"
                             MetricFontSize="24" />
```

## Notes / caveats

- `TargetNullValue={DynamicResource ...}` (used for `ValueForeground`'s fallback) requires Avalonia 11+.
- Label/value rows are two separate `TextBlock`s in a horizontal `StackPanel` (not `Run`s inside one `TextBlock`) — needed so `ShowLabel`/`ShowValue` can toggle independently. Visually near-identical to the old layout, but worth a wrap-check if any panel relied on the old single-block behavior.
- `ContentFontFamily` and `LabelForeground` route through `NullToUnsetConverter` so an unset instance defers to app-level Styles (e.g. a global `Style Selector="TextBlock"` setting a Monospace font) instead of silently overriding them with `null`. `ValueForeground` does NOT use this converter — its null-fallback to `MetricValueBrush` is an intentional default, not meant to defer to Style.
- `ContentFontFamily` is shared across header + rows (one property, not split per element). Ask if you want it split into e.g. `CategoryFontFamily` / `MetricFontFamily`.
- `Text`-kind catalog entries (e.g. `cpu.model`, `os.name`) put their display text in `DisplayValue`, not `Label` — `ShowValue="False"` on these hides the actual content, not just a number.
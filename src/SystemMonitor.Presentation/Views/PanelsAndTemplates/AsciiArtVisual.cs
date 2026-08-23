using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SystemMonitor.Domain.AsciiArt;
using SystemMonitor.Presentation.Theming;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public sealed class AsciiArtVisual : Control
{
    public static readonly StyledProperty<AsciiCell[,]?> ArtProperty =
        AvaloniaProperty.Register<AsciiArtVisual, AsciiCell[,]?>(nameof(Art));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<AsciiArtVisual, bool>(nameof(IsPlaying), true);

    public AsciiCell[,]? Art
    {
        get => GetValue(ArtProperty);
        set => SetValue(ArtProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private const double CellWidth = 6;
    private const double CellHeight = 10;
    private static readonly Typeface Mono = new("Consolas, Cascadia Mono, monospace");
    private static readonly TimeSpan GlintInterval = TimeSpan.FromMilliseconds(40);

    private static readonly Dictionary<(char Glyph, double FontSize), FormattedText> _glyphTextCache = new();

    private AsciiCell[,]? _displayArt;
    private DispatcherTimer? _glintTimer;
    private double _elapsedMs;

    private readonly Dictionary<uint, SolidColorBrush> _brushCache = new();

    static AsciiArtVisual()
    {
        AffectsMeasure<AsciiArtVisual>(ArtProperty);
        AffectsRender<AsciiArtVisual>(IsPlayingProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ArtProperty)
        {
            _displayArt = Clone(change.GetNewValue<AsciiCell[,]?>());
            _brushCache.Clear();
            InvalidateMeasure();
            InvalidateVisual();
        }

        if (change.Property == IsPlayingProperty && change.NewValue is bool isPlaying)
        {
            if (isPlaying)
                StartGlint();
            else
                StopGlint();
        }
    }

    public AsciiArtVisual()
    {
        if (ThemeRuntime.Service is not null)
        {
            ThemeRuntime.Service.ThemeChanged += (_, _) => InvalidateVisual();
        }

        this.AttachedToVisualTree += (_, _) =>
        {
            if (IsPlaying)
                StartGlint();
        };
        this.DetachedFromVisualTree += (_, _) => StopGlint();
    }

    public static double ComputeGlintFactor(int row, int col, double elapsedMs)
    {
        double rowWave = Math.Sin((elapsedMs * 0.0018) + (row * 1.35));
        double columnWave = Math.Sin((elapsedMs * 0.0012) + (col * 0.85) + (row * 0.40));
        double shimmer = Math.Sin((elapsedMs * 0.0024) + (row * 1.8) - (col * 0.65)) * 0.12;

        double factor = 1.0 + (rowWave * 0.18) + (columnWave * 0.10) + shimmer;
        return Math.Clamp(factor, 0.85, 1.25);
    }

    private static AsciiCell[,]? Clone(AsciiCell[,]? source)
    {
        if (source is null) return null;
        var copy = new AsciiCell[source.GetLength(0), source.GetLength(1)];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private void StartGlint()
    {
        if (_glintTimer is not null)
            return;

        _glintTimer = new DispatcherTimer { Interval = GlintInterval };
        _glintTimer.Tick += (_, _) =>
        {
            _elapsedMs += GlintInterval.TotalMilliseconds;
            InvalidateVisual();
        };
        _glintTimer.Start();
    }

    private void StopGlint()
    {
        _glintTimer?.Stop();
        _glintTimer = null;
    }

    private static Color GetThemeMetricColor()
    {
        var service = ThemeRuntime.Service;
        if (service is null)
            return Color.FromRgb(192, 132, 252);

        return ThemeResourceApplier.ToColor(service.CurrentTheme.Chrome.MetricValue);
    }

    private SolidColorBrush GetBrush(Color color)
    {
        uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

        if (_brushCache.TryGetValue(key, out var cached))
            return cached;

        var brush = new SolidColorBrush(color);
        _brushCache[key] = brush;
        return brush;
    }

    private static Color AdjustForGlint(Color baseColor, double factor)
    {
        return Color.FromArgb(
            baseColor.A,
            ClampToByte(baseColor.R * factor),
            ClampToByte(baseColor.G * factor),
            ClampToByte(baseColor.B * factor));
    }

    private static double ComputeFitScale(Size preferredSize, Size availableSize)
    {
        if (preferredSize.Width <= 0 || preferredSize.Height <= 0)
            return 1d;

        if (availableSize.Width <= 0 || availableSize.Height <= 0)
            return 1d;

        double scaleX = availableSize.Width / preferredSize.Width;
        double scaleY = availableSize.Height / preferredSize.Height;
        return Math.Min(scaleX, scaleY);
    }

    private static FormattedText GetOrCreateFormattedText(char glyph, double fontSize)
    {
        var key = (glyph, fontSize);
        if (!_glyphTextCache.TryGetValue(key, out var ft))
        {
            ft = new FormattedText(
                glyph.ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Mono,
                fontSize,
                Brushes.White);
            _glyphTextCache[key] = ft;
        }
        return ft;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var art = _displayArt;
        if (art is null) return;

        var themeColor = GetThemeMetricColor();
        int rows = art.GetLength(0);
        int cols = art.GetLength(1);

        double preferredWidth = cols * CellWidth;
        double preferredHeight = rows * CellHeight;
        var availableBounds = Bounds.Width > 0 && Bounds.Height > 0
            ? Bounds.Size
            : new Size(preferredWidth, preferredHeight);
        double fitScale = ComputeFitScale(new Size(preferredWidth, preferredHeight), availableBounds);

        double cellWidth = CellWidth * fitScale;
        double cellHeight = CellHeight * fitScale;
        double contentWidth = preferredWidth * fitScale;
        double contentHeight = preferredHeight * fitScale;
        double originX = (availableBounds.Width - contentWidth) / 2d;
        double originY = (availableBounds.Height - contentHeight) / 2d;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var cell = art[row, col];
                if (cell.Glyph == ' ') continue;

                double factor = ComputeGlintFactor(row, col, _elapsedMs);
                var brush = GetBrush(AdjustForGlint(themeColor, factor));

                var textSize = Math.Max(1d, CellHeight * fitScale);
                var ft = GetOrCreateFormattedText(cell.Glyph, textSize);
                ft.SetForegroundBrush(brush);

                var origin = new Point(originX + (col * cellWidth), originY + (row * cellHeight));
                context.DrawText(ft, origin);
            }
        }
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var art = _displayArt;
        if (art is null) return new Size(0, 0);

        var preferredSize = new Size(art.GetLength(1) * CellWidth, art.GetLength(0) * CellHeight);

        if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
            return preferredSize;

        return new Size(
            Math.Max(0, availableSize.Width),
            Math.Max(0, availableSize.Height));
    }
}
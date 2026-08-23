using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public sealed class AsciiArtVisual : Control
{
    public static readonly StyledProperty<AsciiCell[,]?> ArtProperty =
        AvaloniaProperty.Register<AsciiArtVisual, AsciiCell[,]?>(nameof(Art));

    public AsciiCell[,]? Art
    {
        get => GetValue(ArtProperty);
        set => SetValue(ArtProperty, value);
    }

    private const double CellWidth = 7;
    private const double CellHeight = 12;
    private static readonly Typeface Mono = new("Consolas, Cascadia Mono, monospace");

   
    private const double FlickerIntervalMs = 145;        
    private const double FlickerFractionPerTick = 0.12;  
    private const int MaxTierJump = 2;                  

    // Cache for reusable FormattedText per glyph (static because font/size are fixed)
    private static readonly Dictionary<char, FormattedText> _glyphTextCache = new();

    private AsciiCell[,]? _displayArt;
    private DispatcherTimer? _flickerTimer;
    private readonly Random _rng = new();

    // Reused across frames instead of allocating per cell per render.
    private readonly Dictionary<uint, SolidColorBrush> _brushCache = new();

    static AsciiArtVisual()
    {
        AffectsMeasure<AsciiArtVisual>(ArtProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ArtProperty)
        {
            _displayArt = Clone(change.GetNewValue<AsciiCell[,]?>());
            // Clear brush cache when art changes to prevent unbounded growth
            _brushCache.Clear();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public AsciiArtVisual()
    {
        this.AttachedToVisualTree += (_, _) => StartFlicker();
        this.DetachedFromVisualTree += (_, _) => StopFlicker();
    }

    private static AsciiCell[,]? Clone(AsciiCell[,]? source)
    {
        if (source is null) return null;
        var copy = new AsciiCell[source.GetLength(0), source.GetLength(1)];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private void StartFlicker()
    {
        if (_flickerTimer is not null)
            return;

        _flickerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FlickerIntervalMs) };
        _flickerTimer.Tick += (_, _) => Flicker();
        _flickerTimer.Start();
    }

    private void StopFlicker()
    {
        _flickerTimer?.Stop();
        _flickerTimer = null;
    }

    private void Flicker()
    {
        var art = _displayArt;
        if (art is null) return;

        int rows = art.GetLength(0);
        int cols = art.GetLength(1);
        int totalCells = rows * cols;
        int perturbCount = Math.Max(1, (int)(totalCells * FlickerFractionPerTick));

        bool changed = false;

        for (int i = 0; i < perturbCount; i++)
        {
            int row = _rng.Next(rows);
            int col = _rng.Next(cols);
            var cell = art[row, col];

            if (cell.Glyph == ' ') continue;

            int jump = _rng.Next(-MaxTierJump, MaxTierJump + 1);
            if (jump == 0) continue;

            int newTier = Math.Clamp(cell.Tier + jump, 0, AsciiGlyphRamp.Tiers.Length - 1);
            if (newTier == cell.Tier) continue;

            art[row, col] = cell with { Glyph = AsciiGlyphRamp.Tiers[newTier], Tier = newTier };
            changed = true;
        }

        if (changed) InvalidateVisual();
    }

    private SolidColorBrush GetBrush(byte r, byte g, byte b)
    {
        // Pack RGB into a single key instead of allocating a Color/tuple as dict key.
        uint key = ((uint)r << 16) | ((uint)g << 8) | b;

        if (_brushCache.TryGetValue(key, out var cached))
            return cached;

        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        _brushCache[key] = brush;
        return brush;
    }

    // Retrieves a cached FormattedText for the given glyph.
    // The brush is temporary and will be overwritten before drawing.
    private FormattedText GetOrCreateFormattedText(char glyph)
    {
        if (!_glyphTextCache.TryGetValue(glyph, out var ft))
        {
            ft = new FormattedText(
                glyph.ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Mono,
                CellHeight,
                Brushes.White);  // dummy brush, replaced before drawing
            _glyphTextCache[glyph] = ft;
        }
        return ft;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var art = _displayArt;
        if (art is null) return;

        int rows = art.GetLength(0);
        int cols = art.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var cell = art[row, col];
                if (cell.Glyph == ' ') continue;

                // Reuse a cached FormattedText for this glyph
                var ft = GetOrCreateFormattedText(cell.Glyph);
                var brush = GetBrush(cell.R, cell.G, cell.B);

                // Update the brush on the cached instance (cheap, no re-layout)
                ft.SetForegroundBrush(brush);

                var origin = new Point(col * CellWidth, row * CellHeight);
                context.DrawText(ft, origin);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var art = _displayArt;
        if (art is null) return new Size(0, 0);
        return new Size(art.GetLength(1) * CellWidth, art.GetLength(0) * CellHeight);
    }
}
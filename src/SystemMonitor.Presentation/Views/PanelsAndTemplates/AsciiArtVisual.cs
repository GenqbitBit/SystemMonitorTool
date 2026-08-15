using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    private const double CellWidth = 3.0;
    private const double CellHeight = 5.0;
    private static readonly Typeface Mono = new("Consolas, Cascadia Mono, monospace");

    static AsciiArtVisual()
    {
        AffectsMeasure<AsciiArtVisual>(ArtProperty);
        AffectsRender<AsciiArtVisual>(ArtProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var art = Art;
        if (art is null) return;

        int rows = art.GetLength(0);
        int cols = art.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var cell = art[row, col];
                if (cell.Glyph == ' ') continue; // skip drawing blank cells

                var brush = new SolidColorBrush(Color.FromRgb(cell.R, cell.G, cell.B));
                var formatted = new FormattedText(
                    cell.Glyph.ToString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Mono,
                    CellHeight,
                    brush);

                var origin = new Point(col * CellWidth, row * CellHeight);
                context.DrawText(formatted, origin);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var art = Art;
        if (art is null) return new Size(0, 0);
        return new Size(art.GetLength(1) * CellWidth, art.GetLength(0) * CellHeight);
    }
}
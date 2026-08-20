using System;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public static class GraphRendererFactory
{
    public static IGraphContentRenderer Create(GraphKind kind, AppSettings settings)
    {
        return (kind, settings.GraphStyle) switch
        {
            (GraphKind.Area, GraphStyle.Block) => new BlockAreaGraphRenderer
            {
                BlockWidth = settings.Block.AreaBlockWidth,
                BlockGap = settings.Block.AreaBlockGap,
                BandCount = settings.BandCount
            },
            (GraphKind.Area, GraphStyle.Braille) => new BrailleAreaGraphRenderer
            {
                CellWidth = settings.Braille.AreaCellWidth,
                CellHeight = settings.Braille.AreaCellHeight,
                FontSize = settings.Braille.AreaFontSize,
                BandCount = settings.BandCount
            },
            (GraphKind.Bar, GraphStyle.Block) => new BlockPercentageBarRenderer
            {
                BlockWidth = settings.Block.BarBlockWidth,
                BlockGap = settings.Block.BarBlockGap
            },
            (GraphKind.Bar, GraphStyle.Braille) => new BraillePercentageBarRenderer
            {
                CellWidth = settings.Braille.BarCellWidth,
                CellHeight = settings.Braille.BarCellHeight,
                FontSize = settings.Braille.BarFontSize
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
namespace SystemMonitor.Domain.Models;

public sealed record AppSettings
{
    public static AppSettings Defaults => new();

    public int TickIntervalMs { get; init; } = 900;

    public GraphStyle GraphStyle { get; init; } = GraphStyle.Block;


    public int BandCount { get; init; } = 4;

    public BlockGraphSettings Block { get; init; } = new();
    public BrailleGraphSettings Braille { get; init; } = new();
}
namespace SystemMonitor.Domain.Models;


public sealed record BrailleGraphSettings
{
    public double AreaCellWidth { get; init; } = 10;
    public double AreaCellHeight { get; init; } = 10;
    public double AreaFontSize { get; init; } = 20;

    public double BarCellWidth { get; init; } = 10;
    public double BarCellHeight { get; init; } = 10;
    public double BarFontSize { get; init; } = 20;
}
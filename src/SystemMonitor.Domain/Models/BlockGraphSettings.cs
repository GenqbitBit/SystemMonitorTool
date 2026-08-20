namespace SystemMonitor.Domain.Models;


public sealed record BlockGraphSettings
{
    public double AreaBlockWidth { get; init; } = 5;
    public double AreaBlockGap { get; init; } = 1;

    public double BarBlockWidth { get; init; } = 2;
    public double BarBlockGap { get; init; } = 1;
}
namespace SystemMonitor.Domain.Models.Theming;


public sealed class ThemeRolePalette
{
    public required ThemeColor Accent1 { get; init; }
    public required ThemeColor Accent2 { get; init; }
    public required ThemeColor Accent3 { get; init; }
    public required ThemeColor Accent4 { get; init; }
    public required ThemeColor Positive { get; init; }
    public required ThemeColor Warning { get; init; }
    public required ThemeColor Critical { get; init; }
}
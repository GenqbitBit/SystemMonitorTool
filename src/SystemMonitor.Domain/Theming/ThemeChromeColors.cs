namespace SystemMonitor.Domain.Models.Theming;


public sealed class ThemeChromeColors
{
    public required ThemeColor WindowBorder { get; init; }
    public required ThemeColor SeparatorLine { get; init; }
    public required ThemeColor LabelBackground { get; init; }
    public required ThemeColor MetricValue { get; init; }
    public required ThemeColor NavTabBackground { get; init; }
    public required ThemeColor NavTabHoverBackground { get; init; }
    public required ThemeColor NavTabActiveBackground { get; init; }
    public required ThemeColor PanelAccentBorder { get; init; }
}
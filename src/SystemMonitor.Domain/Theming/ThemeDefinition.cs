namespace SystemMonitor.Domain.Models.Theming;

public sealed class ThemeDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string BackgroundAsset { get; init; }
    public required ThemeChromeColors Chrome { get; init; }
    public required ThemeRolePalette Roles { get; init; }
}
// SystemMonitor.Infrastructure/Theming/BuiltInThemes.cs
using System.Collections.Generic;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Infrastructure.Theming;

/// <summary>
/// Built-in theme catalog. To add a new theme: define it below and add it
/// to `All`. Nothing else in the app needs to change.
/// </summary>
public static class BuiltInThemes
{
    
    public static readonly ThemeDefinition NeonDark = new()
    {
        Id = "neon-dark",
        DisplayName = "Neon Dark",
        Chrome = new ThemeChromeColors
        {
            WindowBorder = ThemeColor.FromHex("#FFFFFF"),
            SeparatorLine = ThemeColor.FromHex("#F6F6F6"),
            LabelBackground = ThemeColor.FromHex("#000000"),
            MetricValue = ThemeColor.FromHex("#19CB90"),
            NavTabBackground = ThemeColor.FromHex("#000000"),
            NavTabHoverBackground = ThemeColor.FromHex("#2A4FB0"),
            NavTabActiveBackground = ThemeColor.FromHex("#2A4FB0"),
            PanelAccentBorder = ThemeColor.FromHex("#DF20ED"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#4D03F8"), 
            Accent2 = ThemeColor.FromHex("#03F844"),
            Accent3 = ThemeColor.FromHex("#EC03F8"), 
            Accent4 = ThemeColor.FromHex("#0320F8"), 
            Positive = ThemeColor.FromHex("#22C55E"),
            Warning = ThemeColor.FromHex("#FFA201"),
            Critical = ThemeColor.FromHex("#DB0909"),
        },
    };

    
    public static readonly ThemeDefinition OceanLight = new()
    {
        Id = "ocean-light",
        DisplayName = "Ocean Light",
        Chrome = new ThemeChromeColors
        {
            WindowBorder = ThemeColor.FromHex("#3A4A5C"),
            SeparatorLine = ThemeColor.FromHex("#D8E3EA"),
            LabelBackground = ThemeColor.FromHex("#1B2A38"),
            MetricValue = ThemeColor.FromHex("#0FA3B1"),
            NavTabBackground = ThemeColor.FromHex("#1B2A38"),
            NavTabHoverBackground = ThemeColor.FromHex("#146C94"),
            NavTabActiveBackground = ThemeColor.FromHex("#0F5C82"),
            PanelAccentBorder = ThemeColor.FromHex("#19A7CE"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#146C94"),
            Accent2 = ThemeColor.FromHex("#19A7CE"),
            Accent3 = ThemeColor.FromHex("#5AB2FF"),
            Accent4 = ThemeColor.FromHex("#0FA3B1"),
            Positive = ThemeColor.FromHex("#2ECC71"),
            Warning = ThemeColor.FromHex("#F4A300"),
            Critical = ThemeColor.FromHex("#E63946"),
        },
    };

    public static readonly IReadOnlyList<ThemeDefinition> All = new[]
    {
        NeonDark,
        OceanLight,
    };
}
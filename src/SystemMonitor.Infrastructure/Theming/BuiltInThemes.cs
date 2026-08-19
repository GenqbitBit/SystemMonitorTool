using System.Collections.Generic;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Infrastructure.Theming;

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
            MetricValue = ThemeColor.FromHex("#C084FC"),
            NavTabBackground = ThemeColor.FromHex("#000000"),
            NavTabHoverBackground = ThemeColor.FromHex("#4C1D95"),
            NavTabActiveBackground = ThemeColor.FromHex("#6D28D9"),
            PanelAccentBorder = ThemeColor.FromHex("#A855F7"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#B366F0"),
            Accent2 = ThemeColor.FromHex("#9333EA"),
            Accent3 = ThemeColor.FromHex("#6D28D9"),
            Accent4 = ThemeColor.FromHex("#4C1D95"),
            Positive = ThemeColor.FromHex("#C4A0FF"),
            Warning = ThemeColor.FromHex("#5B21B6"),
            Critical = ThemeColor.FromHex("#2E1065"),
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
            Accent1 = ThemeColor.FromHex("#125F82"),
            Accent2 = ThemeColor.FromHex("#1389AC"),
            Accent3 = ThemeColor.FromHex("#3D8FDE"),
            Accent4 = ThemeColor.FromHex("#0C8390"),
            Positive = ThemeColor.FromHex("#4FB3DE"),
            Warning = ThemeColor.FromHex("#043A54"),
            Critical = ThemeColor.FromHex("#04141C"),
        },
    };

    public static readonly ThemeDefinition BrownCoffee = new()
    {
        Id = "brown-coffee",
        DisplayName = "Brown Coffee",
        Chrome = new ThemeChromeColors
        {
            WindowBorder = ThemeColor.FromHex("#D7C4B7"),
            SeparatorLine = ThemeColor.FromHex("#3D2E2B"),
            LabelBackground = ThemeColor.FromHex("#120C0A"),
            MetricValue = ThemeColor.FromHex("#E2A76F"),
            NavTabBackground = ThemeColor.FromHex("#120C0A"),
            NavTabHoverBackground = ThemeColor.FromHex("#3D2A24"),
            NavTabActiveBackground = ThemeColor.FromHex("#5C3A21"),
            PanelAccentBorder = ThemeColor.FromHex("#A0522D"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#E2A76F"),
            Accent2 = ThemeColor.FromHex("#C87D55"),
            Accent3 = ThemeColor.FromHex("#8C6239"),
            Accent4 = ThemeColor.FromHex("#D7C4B7"),
            Positive = ThemeColor.FromHex("#F4E0C8"),
            Warning = ThemeColor.FromHex("#6B4423"),
            Critical = ThemeColor.FromHex("#2A1A0F"),
        },
    };

    public static readonly IReadOnlyList<ThemeDefinition> All = new[]
    {
        NeonDark,
        OceanLight,
        BrownCoffee,
    };
}
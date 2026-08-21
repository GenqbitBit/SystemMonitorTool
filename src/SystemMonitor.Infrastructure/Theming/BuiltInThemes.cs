using System.Collections.Generic;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Infrastructure.Theming;

public static class BuiltInThemes
{
    public static readonly ThemeDefinition AmethystDark = new()
    {
        Id = "amethyst-dark",
        DisplayName = "Amethyst Dark",
        BackgroundAsset = "purple background.gif",
        Chrome = new ThemeChromeColors
        {
            Background = ThemeColor.FromHex("#321a36"),
            WindowBorder = ThemeColor.FromHex("#5B21B6"),
            SeparatorLine = ThemeColor.FromHex("#2E1065"),
            LabelBackground = ThemeColor.FromHex("#000000"),
            MetricValue = ThemeColor.FromHex("#C084FC"),
            NavTabBackground = ThemeColor.FromHex("#321a36"),
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

    public static readonly ThemeDefinition CyberSlate = new()
    {
        Id = "cyber-slate",
        DisplayName = "Cyber Slate",
        BackgroundAsset = "Cyber.gif",
        Chrome = new ThemeChromeColors
        {
            Background = ThemeColor.FromHex("#0C0A14"),
            WindowBorder = ThemeColor.FromHex("#8C2E86"),
            SeparatorLine = ThemeColor.FromHex("#1e6d75"),
            LabelBackground = ThemeColor.FromHex("#0C0A14"),
            MetricValue = ThemeColor.FromHex("#3ECBDA"),
            NavTabBackground = ThemeColor.FromHex("#0C0A14"),
            NavTabHoverBackground = ThemeColor.FromHex("#211B38"),
            NavTabActiveBackground = ThemeColor.FromHex("#3D2C7A"),
            PanelAccentBorder = ThemeColor.FromHex("#7A4FB8"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#3ECBDA"),
            Accent2 = ThemeColor.FromHex("#7A4FB8"),
            Accent3 = ThemeColor.FromHex("#8C2E86"),
            Accent4 = ThemeColor.FromHex("#3D2C7A"),
            Positive = ThemeColor.FromHex("#3FCFA0"),
            Warning = ThemeColor.FromHex("#D9B23C"),
            Critical = ThemeColor.FromHex("#8C2E86"),
        },
    };

    public static readonly ThemeDefinition Nordic = new()
    {
        Id = "nordic",
        DisplayName = "Nordic",
        BackgroundAsset = "nordic.gif",
        Chrome = new ThemeChromeColors
        {
            Background = ThemeColor.FromHex("#2E3440"),
            WindowBorder = ThemeColor.FromHex("#4C566A"),
            SeparatorLine = ThemeColor.FromHex("#3e4757"),
            LabelBackground = ThemeColor.FromHex("#2E3440"),
            MetricValue = ThemeColor.FromHex("#88C0D0"),
            NavTabBackground = ThemeColor.FromHex("#2E3440"),
            NavTabHoverBackground = ThemeColor.FromHex("#3B4252"),
            NavTabActiveBackground = ThemeColor.FromHex("#434C5E"),
            PanelAccentBorder = ThemeColor.FromHex("#5E81AC"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#88C0D0"),
            Accent2 = ThemeColor.FromHex("#81A1C1"),
            Accent3 = ThemeColor.FromHex("#5E81AC"),
            Accent4 = ThemeColor.FromHex("#B48EAD"),
            Positive = ThemeColor.FromHex("#A3BE8C"),
            Warning = ThemeColor.FromHex("#EBCB8B"),
            Critical = ThemeColor.FromHex("#BF616A"),
        },
    };

    public static readonly ThemeDefinition OceanLight = new()
    {
        Id = "ocean-light",
        DisplayName = "Ocean Light",
        BackgroundAsset = "ocean background.gif",
        Chrome = new ThemeChromeColors
        {
            Background = ThemeColor.FromHex("#1B2A38"),
            WindowBorder = ThemeColor.FromHex("#146C94"),
            SeparatorLine = ThemeColor.FromHex("#043A54"),
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
        BackgroundAsset = "coffee brown.gif",
        Chrome = new ThemeChromeColors
        {
            Background = ThemeColor.FromHex("#3D2A24"),
            WindowBorder = ThemeColor.FromHex("#3D2E2B"),
            SeparatorLine = ThemeColor.FromHex("#6B4423"),
            LabelBackground = ThemeColor.FromHex("#120C0A"),
            MetricValue = ThemeColor.FromHex("#E2A76F"),
            NavTabBackground = ThemeColor.FromHex("#3D2A24"),
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

    public static readonly ThemeDefinition RoseQuartz = new()
    {
        Id = "rose-quartz",
        DisplayName = "The Ferdy Special",
        BackgroundAsset = "pink.gif",
        Chrome = new ThemeChromeColors
        {
            Background = ThemeColor.FromHex("#3B1028"),
            WindowBorder = ThemeColor.FromHex("#F9A8D4"),
            SeparatorLine = ThemeColor.FromHex("#831843"),
            LabelBackground = ThemeColor.FromHex("#3B1028"),
            MetricValue = ThemeColor.FromHex("#F472B6"),
            NavTabBackground = ThemeColor.FromHex("#3B1028"),
            NavTabHoverBackground = ThemeColor.FromHex("#831843"),
            NavTabActiveBackground = ThemeColor.FromHex("#BE185D"),
            PanelAccentBorder = ThemeColor.FromHex("#EC4899"),
        },
        Roles = new ThemeRolePalette
        {
            Accent1 = ThemeColor.FromHex("#F472B6"),
            Accent2 = ThemeColor.FromHex("#EC4899"),
            Accent3 = ThemeColor.FromHex("#BE185D"),
            Accent4 = ThemeColor.FromHex("#831843"),
            Positive = ThemeColor.FromHex("#86EFAC"),
            Warning = ThemeColor.FromHex("#FDE047"),
            Critical = ThemeColor.FromHex("#9F1239"),
        },
    };

    public static readonly IReadOnlyList<ThemeDefinition> All = new[]
    {
        AmethystDark,
        OceanLight,
        BrownCoffee,
        CyberSlate,
        Nordic,
        RoseQuartz,
    };
}
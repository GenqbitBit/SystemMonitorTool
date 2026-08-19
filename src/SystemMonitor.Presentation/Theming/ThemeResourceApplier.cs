using Avalonia;
using Avalonia.Media;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Presentation.Theming;

public static class ThemeResourceApplier
{
    public static void Apply(ThemeDefinition theme)
    {
        var resources = Avalonia.Application.Current!.Resources;
        var chrome = theme.Chrome;
        var roles = theme.Roles;

        resources["WindowBorderBrush"] = ToBrush(chrome.WindowBorder);
        resources["SeparatorBrush"] = ToBrush(chrome.SeparatorLine);
        resources["LabelBackgroundBrush"] = ToBrush(chrome.LabelBackground);
        resources["MetricValueBrush"] = ToBrush(chrome.MetricValue);
        resources["NavTabBackground"] = ToBrush(chrome.NavTabBackground);
        resources["NavTabHoverBackground"] = ToBrush(chrome.NavTabHoverBackground);
        resources["NavTabActiveBackground"] = ToBrush(chrome.NavTabActiveBackground);
        resources["PanelAccentBorderBrush"] = ToBrush(chrome.PanelAccentBorder);

        resources["Accent1Brush"] = ToBrush(roles.Accent1);
        resources["Accent2Brush"] = ToBrush(roles.Accent2);
        resources["Accent3Brush"] = ToBrush(roles.Accent3);
        resources["Accent4Brush"] = ToBrush(roles.Accent4);
        resources["PositiveBrush"] = ToBrush(roles.Positive);
        resources["WarningBrush"] = ToBrush(roles.Warning);
        resources["CriticalBrush"] = ToBrush(roles.Critical);
    }

    private static SolidColorBrush ToBrush(ThemeColor c) =>
        new(Color.FromArgb(c.A, c.R, c.G, c.B));

    public static Color ToColor(ThemeColor c) =>
        Color.FromArgb(c.A, c.R, c.G, c.B);
}
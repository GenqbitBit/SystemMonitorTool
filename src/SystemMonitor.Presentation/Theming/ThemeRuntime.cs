using SystemMonitor.Application.Interfaces;

namespace SystemMonitor.Presentation.Theming;

/// <summary>
/// Set once in App.OnFrameworkInitializationCompleted. Lets Controls/Windows
/// that live in the visual tree (not the DI container) reach IThemeService.
/// </summary>
public static class ThemeRuntime
{
    public static IThemeService Service { get; set; } = null!;
}
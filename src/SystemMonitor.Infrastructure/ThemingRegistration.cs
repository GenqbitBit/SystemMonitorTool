using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Theming;

namespace SystemMonitor.Infrastructure;

public static class ThemingRegistration
{
    public static IServiceCollection AddThemingServices(this IServiceCollection services)
    {
        services.AddSingleton<ThemeRegistry>();
        services.AddSingleton<IThemeSettingsStore, JsonThemeSettingsStore>();
        services.AddSingleton<IThemeService, ThemeService>();
        return services;
    }
}
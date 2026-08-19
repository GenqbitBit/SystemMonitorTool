using System;
using System.Collections.Generic;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Infrastructure.Theming;

public class ThemeService : IThemeService
{
    private readonly ThemeRegistry _registry;
    private readonly IThemeSettingsStore _settingsStore;

    public ThemeService(ThemeRegistry registry, IThemeSettingsStore settingsStore)
    {
        _registry = registry;
        _settingsStore = settingsStore;

        var saved = _settingsStore.Load();
        CurrentTheme = (saved is not null ? _registry.FindById(saved.SelectedThemeId) : null)
            ?? _registry.Default;
    }

    public ThemeDefinition CurrentTheme { get; private set; }

    public IReadOnlyList<ThemeDefinition> AvailableThemes => _registry.All;

    public event EventHandler<ThemeDefinition>? ThemeChanged;

    public void SetTheme(string themeId)
    {
        var theme = _registry.FindById(themeId);
        if (theme is null || theme.Id == CurrentTheme.Id)
            return;

        CurrentTheme = theme;
        _settingsStore.Save(new ThemeSettings { SelectedThemeId = theme.Id });
        ThemeChanged?.Invoke(this, theme);
    }
}
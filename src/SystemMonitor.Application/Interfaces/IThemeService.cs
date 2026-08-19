using System;
using System.Collections.Generic;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Application.Interfaces;

public interface IThemeService
{
    ThemeDefinition CurrentTheme { get; }
    IReadOnlyList<ThemeDefinition> AvailableThemes { get; }

    void SetTheme(string themeId);

    
    event EventHandler<ThemeDefinition>? ThemeChanged;
}
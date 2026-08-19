using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Application.Interfaces;

public interface IThemeSettingsStore
{
    
    ThemeSettings? Load();
    void Save(ThemeSettings settings);
}
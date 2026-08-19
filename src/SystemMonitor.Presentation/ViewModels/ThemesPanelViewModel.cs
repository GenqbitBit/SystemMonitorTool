using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models.Theming;
using SystemMonitor.Presentation.Theming;

namespace SystemMonitor.Presentation.ViewModels;

public partial class ThemesPanelViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    public ThemesPanelViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        AvailableThemes = new ObservableCollection<ThemeDefinition>(_themeService.AvailableThemes);
        _selectedTheme = _themeService.CurrentTheme;
    }

    public ObservableCollection<ThemeDefinition> AvailableThemes { get; }

    [ObservableProperty]
    private ThemeDefinition _selectedTheme;

    partial void OnSelectedThemeChanged(ThemeDefinition value)
    {
        _themeService.SetTheme(value.Id);
    }
}
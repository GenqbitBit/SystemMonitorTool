using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using SystemMonitor.Presentation.ViewModels;
using SystemMonitor.Presentation.Views;

using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Monitoring;
using SystemMonitor.Infrastructure;
using SystemMonitor.Infrastructure.Persistence;
using SystemMonitor.Domain.AsciiArt;
using SystemMonitor.Presentation.Views.PanelsAndTemplates;
using SystemMonitor.Application.AsciiArt;

namespace SystemMonitor.Presentation;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddPlatformMonitoringServices();
        services.AddThemingServices();              
        services.AddSingleton<ISettingsService>(_ => new JsonSettingsService(
            Path.Combine(AppContext.BaseDirectory, "settings.json")));
        services.AddTransient<MetricsTableViewModel>();
        services.AddSingleton<IAsciiArtConverter, AsciiArtConverter>();
        var provider = services.BuildServiceProvider();

        var themeService = provider.GetRequiredService<IThemeService>();  
        SystemMonitor.Presentation.Theming.ThemeRuntime.Service = themeService; 
        SystemMonitor.Presentation.Theming.ThemeResourceApplier.Apply(themeService.CurrentTheme); 
        themeService.ThemeChanged += (_, theme) =>
            SystemMonitor.Presentation.Theming.ThemeResourceApplier.Apply(theme); 
        SystemMonitor.Presentation.Common.SettingsRuntime.Initialize(
            provider.GetRequiredService<ISettingsService>());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    provider.GetRequiredService<ISettingsService>(),
                    provider.GetRequiredService<IMetricHistoryStore>()),
            };
            desktop.MainWindow = mainWindow;

            var mainViewModel = (MainWindowViewModel)mainWindow.DataContext;
            AsciiArtPanelViewModel? asciiArtViewModel = null;
            var backendInitialization = mainViewModel.InitializeBackendAsync(provider);

            desktop.ShutdownRequested += async (_, _) =>
            {
                mainViewModel?.Dispose();
                asciiArtViewModel?.Dispose();
                await backendInitialization;
                provider.Dispose();
            };

            var converter = provider.GetRequiredService<IAsciiArtConverter>();
            asciiArtViewModel = new AsciiArtPanelViewModel(converter, mainWindow.StorageProvider);
            mainWindow.AsciiPanel.DataContext = asciiArtViewModel;
        }

        base.OnFrameworkInitializationCompleted();
    }

}
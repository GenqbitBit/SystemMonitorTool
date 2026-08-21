using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
        services.AddTransient<MainWindowViewModel>();
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
                DataContext = null,
            };
            desktop.MainWindow = mainWindow;

            MainWindowViewModel? mainViewModel = null;
            AsciiArtPanelViewModel? asciiArtViewModel = null;
            desktop.ShutdownRequested += (_, _) =>
            {
                mainViewModel?.Dispose();
                asciiArtViewModel?.Dispose();
                provider.Dispose();
            };

            InitializeMainWindowAsync(mainWindow, provider, viewModel =>
            {
                mainViewModel = viewModel;
                mainWindow.DataContext = viewModel;

                var converter = provider.GetRequiredService<IAsciiArtConverter>();
                asciiArtViewModel = new AsciiArtPanelViewModel(converter, mainWindow.StorageProvider);
                mainWindow.AsciiPanel.DataContext = asciiArtViewModel;
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async void InitializeMainWindowAsync(
        MainWindow mainWindow,
        ServiceProvider provider,
        Action<MainWindowViewModel> applyViewModel)
    {
        try
        {
            var viewModel = await Task.Run(provider.GetRequiredService<MainWindowViewModel>);

            Dispatcher.UIThread.Post(() =>
            {
                applyViewModel(viewModel);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => mainWindow.Title = $"Startup failed: {ex.Message}");
        }
    }
}
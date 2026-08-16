using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SystemMonitor.Presentation.ViewModels;
using SystemMonitor.Presentation.Views;

using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Monitoring;
using SystemMonitor.Infrastructure;
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
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MetricsTableViewModel>();
        services.AddSingleton<IAsciiArtConverter, AsciiArtConverter>();
        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>(),
            };

            var converter = provider.GetRequiredService<IAsciiArtConverter>();
            mainWindow.AsciiPanel.DataContext =
                new AsciiArtPanelViewModel(converter, mainWindow.StorageProvider);

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
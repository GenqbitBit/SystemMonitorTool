using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SystemMonitor.Presentation.ViewModels;
using SystemMonitor.Presentation.Views;

using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure.Monitoring;


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
        services.AddSingleton<ICpuMonitorService, CpuMonitorService>();
        services.AddSingleton<IMemoryMonitorService, MemoryMonitorService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<IDiskMonitorService, DiskMonitorService>();
        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
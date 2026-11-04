using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Presentation.ViewModels;
using SystemMonitor.Presentation.Views;
using SystemMonitor.Presentation.Services;

using Microsoft.Extensions.DependencyInjection;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Infrastructure;
using SystemMonitor.Infrastructure.DependencyInjection;
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
            var startupCancellation = new CancellationTokenSource();
            var backendInitialization = mainViewModel.InitializeBackendAsync(provider, startupCancellation.Token);

            var converter = provider.GetRequiredService<IAsciiArtConverter>();
            var asciiArtViewModel = new AsciiArtPanelViewModel(converter, mainWindow.StorageProvider);
            mainWindow.AsciiPanel.DataContext = asciiArtViewModel;

            // Deterministic shutdown: cancel startup, let the (possibly
            // half-finished) backend attach settle, stop polling, then drain
            // SQLite and close the LibreHardwareMonitor driver. The previous
            // fire-and-forget handler let the process exit before queued
            // events were flushed.
            var shutdownStarted = 0;
            mainWindow.Closing += (_, e) =>
            {
                if (Interlocked.Exchange(ref shutdownStarted, 1) != 0)
                    return;

                e.Cancel = true;
                Dispatcher.UIThread.Post(() => CompleteShutdownAsync(
                    desktop,
                    provider,
                    startupCancellation,
                    backendInitialization,
                    mainViewModel,
                    asciiArtViewModel));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async void CompleteShutdownAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        ServiceProvider provider,
        CancellationTokenSource startupCancellation,
        Task backendInitialization,
        MainWindowViewModel mainViewModel,
        AsciiArtPanelViewModel asciiArtViewModel)
    {
        try
        {
            startupCancellation.Cancel();

            // Never block the UI thread here: backend initialization resumes
            // through Dispatcher.UIThread.InvokeAsync, so a blocking Wait()
            // would deadlock. Awaiting keeps the dispatcher pumping.
            var initializationFinished = await Task.WhenAny(
                backendInitialization,
                Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(true) == backendInitialization;

            if (!initializationFinished)
                Trace.WriteLine("Backend initialization did not finish before shutdown timeout.");

            try { mainViewModel.Dispose(); }
            catch (Exception ex) { Trace.WriteLine($"ViewModel shutdown failed: {ex}"); }

            try { asciiArtViewModel.Dispose(); }
            catch (Exception ex) { Trace.WriteLine($"ASCII panel shutdown failed: {ex}"); }

            try { provider.Dispose(); }
            catch (Exception ex) { Trace.WriteLine($"Service shutdown failed: {ex}"); }

            startupCancellation.Dispose();
        }
        finally
        {
            desktop.Shutdown();
        }
    }
}

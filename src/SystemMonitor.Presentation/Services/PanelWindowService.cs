using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Presentation.Views.Subwindows.Settings;
using SystemMonitor.Presentation.Views.Subwindows.DataTable;
using SystemMonitor.Presentation.Views.Subwindows.TopProcesses;
using SystemMonitor.Presentation.Views.Subwindows.Themes;
using SystemMonitor.Presentation.Views.Subwindows.Logs;
using SystemMonitor.Presentation.ViewModels;

namespace SystemMonitor.Presentation.Services;

public class PanelWindowService : IPanelWindowService
{

    public static class PanelLabels
    {
        public const string Settings = "Settings";
        public const string Themes = "Themes";
        public const string Logs = "Logs";
        public const string ViewDataTable = "View Data Table";
        public const string ViewTopProcesses = "View Top Processes";
    }

    private readonly Dictionary<string, Window> _openWindows = new();
    private readonly Dictionary<string, PixelPoint> _lastNormalPositions = new();

    public void TogglePanel(string label, Window owner)
    {
        if (_openWindows.TryGetValue(label, out var existing))
        {
   
            if (existing.WindowState == WindowState.Normal)
            {
                _lastNormalPositions[label] = existing.Position;
            }

            existing.Close();
            return;
        }

        Window? window = CreateWindow(label, owner);
        if (window is null)
        {
            return;
        }


        if (_lastNormalPositions.TryGetValue(label, out var savedPosition))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = savedPosition;
        }

        window.PropertyChanged += (_, e) =>
        {

            if (e.Property.Name == nameof(Window.Position) &&
                window.WindowState == WindowState.Normal)
            {
                _lastNormalPositions[label] = window.Position;
            }
            else if (e.Property.Name == nameof(Window.WindowState))
            {
                if (window.WindowState == WindowState.Normal)
                {
                    if (_lastNormalPositions.TryGetValue(label, out var lastPosition))
                    {
                        window.Position = lastPosition;
                    }
                }
            }
        };

        window.Closing += (_, _) =>
        {
            if (window.WindowState == WindowState.Normal)
            {
                _lastNormalPositions[label] = window.Position;
            }
        };

        window.Closed += (_, _) =>
        {
            _openWindows.Remove(label);
        };

        _openWindows[label] = window;
        window.Show(owner);
    }

    private static Window? CreateWindow(string label, Window owner)
    {
        var vm = owner.DataContext as MainWindowViewModel;

        return label switch
        {
            PanelLabels.Settings => new SettingsWindow(),
            PanelLabels.Themes => new ThemesWindow(),
            PanelLabels.Logs => new LogsWindow(),
            PanelLabels.ViewDataTable => vm is not null
                ? new DataTableWindow { DataContext = vm.MetricsTable }
                : null,
            PanelLabels.ViewTopProcesses => vm is not null
                ? new TopProcessesWindow { DataContext = vm }
                : null,
            _ => null
        };
    }
}
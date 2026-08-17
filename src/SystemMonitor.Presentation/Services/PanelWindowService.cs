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
    private readonly Dictionary<string, Window> _openWindows = new();
    private readonly Dictionary<string, PixelPoint> _lastPositions = new();

    public void TogglePanel(string label, Window owner)
    {
        if (_openWindows.TryGetValue(label, out var existing))
        {
            existing.Close();
            return;
        }

        var vm = owner.DataContext as MainWindowViewModel;

        Window? window = label switch
        {
            "Settings" => new SettingsWindow(),
            "Themes" => new ThemesWindow(),
            "Logs" => new LogsWindow(),
            "View Data Table" => vm is not null
                ? new DataTableWindow { DataContext = vm.MetricsTable }
                : null,
            "View Top Processes" => vm is not null
                ? new TopProcessesWindow { DataContext = vm }
                : null,
            _ => null
        };

        if (window is null)
            return;

        if (_lastPositions.TryGetValue(label, out var savedPosition))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = savedPosition;
        }

        // Capture position here, before the window (and its platform handle)
        // is torn down — by the time Closed fires, Position is no longer
        // reliable and tends to read back as (0,0).
        window.Closing += (_, _) =>
        {
            _lastPositions[label] = window.Position;
        };

        window.Closed += (_, _) =>
        {
            _openWindows.Remove(label);
        };

        _openWindows[label] = window;
        window.Show(owner);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Presentation.Views.Subwindows.Settings;
using SystemMonitor.Presentation.Theming;
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

    public void TogglePanel(string label, Window owner)
    {
        if (_openWindows.TryGetValue(label, out var existing))
        {
            CloseTrackedWindow(label, existing);
            return;
        }

        var window = CreateWindow(label, owner);
        if (window is null)
            return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = ComputeCenteredPosition(owner, window);

        AttachWindowLifecycle(label, owner, window);
        _openWindows[label] = window;

        PrepareSpecialPanelState(label, owner, isActive: true);
        window.Show(owner);
        window.Activate();
    }

    public void CloseAllPanels()
    {
        foreach (var panel in _openWindows.ToList())
        {
            var (_, window) = panel;
            if (window.IsVisible)
                window.Close();
            else
                RemoveWindow(panel.Key, window);
        }
    }

    private void CloseTrackedWindow(string label, Window existing)
    {
        PrepareSpecialPanelState(label, existing.Owner as Window, isActive: false);
        existing.Close();
    }

    private void AttachWindowLifecycle(string label, Window owner, Window window)
    {
        window.Closed += OnWindowClosed;

        void OnWindowClosed(object? sender, EventArgs e)
        {
            window.Closed -= OnWindowClosed;

            PrepareSpecialPanelState(label, owner, isActive: false);
            RemoveWindow(label, window);
        }
    }

    private void RemoveWindow(string label, Window window)
    {
        if (_openWindows.TryGetValue(label, out var tracked) && tracked == window)
            _openWindows.Remove(label);
    }

    private static PixelPoint ComputeCenteredPosition(Window owner, Window child)
    {
        var ownerBounds = owner.Bounds;
        var childWidth = child.Width > 0 ? child.Width : child.Bounds.Width;
        var childHeight = child.Height > 0 ? child.Height : child.Bounds.Height;

        var centeredX = owner.Position.X + (ownerBounds.Width - childWidth) / 2;
        var centeredY = owner.Position.Y + (ownerBounds.Height - childHeight) / 2;

        var screen = owner.Screens.ScreenFromVisual(owner) ?? owner.Screens.Primary;
        if (screen is null)
            return owner.Position;

        var workingArea = screen.WorkingArea;

        var minX = workingArea.X;
        var maxX = Math.Max(minX, workingArea.Right - childWidth);
        var minY = workingArea.Y;
        var maxY = Math.Max(minY, workingArea.Bottom - childHeight);

        return new PixelPoint(
            (int)Math.Round(Math.Clamp(centeredX, minX, maxX)),
            (int)Math.Round(Math.Clamp(centeredY, minY, maxY)));
    }

    private static void PrepareSpecialPanelState(string label, Window? owner, bool isActive)
    {
        if (owner?.DataContext is not MainWindowViewModel ownerVm)
            return;

        ownerVm.SetPanelNavState(label, isActive);

        if (label == PanelLabels.ViewDataTable)
            ownerVm.MetricsTable.SetActive(isActive, ownerVm.LatestSnapshot);
    }

    private static Window? CreateWindow(string label, Window owner)
    {
        var vm = owner.DataContext as MainWindowViewModel;

        return label switch
        {
            PanelLabels.Settings => vm is not null
                ? new SettingsWindow
                {
                    DataContext = new SettingsPanelViewModel(vm.Settings),
                    WindowStartupLocation = WindowStartupLocation.Manual,
                }
                : null,
            PanelLabels.Themes => new ThemesWindow
                {
                    DataContext = new ThemesPanelViewModel(ThemeRuntime.Service),
                    WindowStartupLocation = WindowStartupLocation.Manual,
                },
            PanelLabels.Logs => vm is not null
                ? new LogsWindow { DataContext = vm.LogsPanel, WindowStartupLocation = WindowStartupLocation.Manual }
                : null,
            PanelLabels.ViewDataTable => vm is not null
                ? new DataTableWindow { DataContext = vm.MetricsTable, WindowStartupLocation = WindowStartupLocation.Manual }
                : null,
            PanelLabels.ViewTopProcesses => vm is not null
                ? new TopProcessesWindow { DataContext = vm, WindowStartupLocation = WindowStartupLocation.Manual }
                : null,
            _ => null
        };
    }
}
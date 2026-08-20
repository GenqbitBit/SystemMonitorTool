using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

public partial class SettingsPanelViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private bool _suppressUpdates; // guards against re-entrant writes while loading Current into the properties below

    [ObservableProperty]
    private int tickIntervalMs;

    [ObservableProperty]
    private GraphStyle graphStyle;

    [ObservableProperty]
    private int bandCount;

    // Block knobs
    [ObservableProperty]
    private double areaBlockWidth;
    [ObservableProperty]
    private double areaBlockGap;
    [ObservableProperty]
    private double barBlockWidth;
    [ObservableProperty]
    private double barBlockGap;

    // Braille knobs
    [ObservableProperty]
    private double areaCellWidth;
    [ObservableProperty]
    private double areaCellHeight;
    [ObservableProperty]
    private double areaFontSize;
    [ObservableProperty]
    private double barCellWidth;
    [ObservableProperty]
    private double barCellHeight;
    [ObservableProperty]
    private double barFontSize;

    public bool IsBlockStyle => GraphStyle == GraphStyle.Block;
    public bool IsBrailleStyle => GraphStyle == GraphStyle.Braille;

    public GraphStyle[] GraphStyleOptions { get; } = (GraphStyle[])Enum.GetValues(typeof(GraphStyle));

    public SettingsPanelViewModel()
        : this(new Infrastructure.Persistence.JsonSettingsService(
            System.IO.Path.Combine(AppContext.BaseDirectory, "settings.json")))
    {
    }

    public SettingsPanelViewModel(ISettingsService settings)
    {
        _settings = settings;
        LoadFrom(_settings.Current);
    }

    private void LoadFrom(AppSettings s)
    {
        _suppressUpdates = true;

        TickIntervalMs = s.TickIntervalMs;
        GraphStyle = s.GraphStyle;
        BandCount = s.BandCount;

        AreaBlockWidth = s.Block.AreaBlockWidth;
        AreaBlockGap = s.Block.AreaBlockGap;
        BarBlockWidth = s.Block.BarBlockWidth;
        BarBlockGap = s.Block.BarBlockGap;

        AreaCellWidth = s.Braille.AreaCellWidth;
        AreaCellHeight = s.Braille.AreaCellHeight;
        AreaFontSize = s.Braille.AreaFontSize;
        BarCellWidth = s.Braille.BarCellWidth;
        BarCellHeight = s.Braille.BarCellHeight;
        BarFontSize = s.Braille.BarFontSize;

        _suppressUpdates = false;
    }

    partial void OnTickIntervalMsChanged(int value)
    {
        if (_suppressUpdates) return;
        var clamped = Math.Clamp(value, 500, 5000);
        _settings.Update(s => s with { TickIntervalMs = clamped });
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        // Preserve the current GraphStyle selection while resetting all other settings
        var currentGraphStyle = GraphStyle;
        var defaults = AppSettings.Defaults;
        LoadFrom(defaults with { GraphStyle = currentGraphStyle });
        _settings.ResetToDefaults();
    }

    partial void OnGraphStyleChanged(GraphStyle value)
    {
        OnPropertyChanged(nameof(IsBlockStyle));
        OnPropertyChanged(nameof(IsBrailleStyle));
        if (_suppressUpdates) return;
        _settings.Update(s => s with { GraphStyle = value });
    }

    partial void OnBandCountChanged(int value)
    {
        if (_suppressUpdates) return;
        var clamped = Math.Clamp(value, 2, 20);
        _settings.Update(s => s with { BandCount = clamped });
    }

    partial void OnAreaBlockWidthChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Block = s.Block with { AreaBlockWidth = value } });
    }

    partial void OnAreaBlockGapChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Block = s.Block with { AreaBlockGap = value } });
    }

    partial void OnBarBlockWidthChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Block = s.Block with { BarBlockWidth = value } });
    }

    partial void OnBarBlockGapChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Block = s.Block with { BarBlockGap = value } });
    }

    partial void OnAreaCellWidthChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Braille = s.Braille with { AreaCellWidth = value } });
    }

    partial void OnAreaCellHeightChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Braille = s.Braille with { AreaCellHeight = value } });
    }

    partial void OnAreaFontSizeChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Braille = s.Braille with { AreaFontSize = value } });
    }

    partial void OnBarCellWidthChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Braille = s.Braille with { BarCellWidth = value } });
    }

    partial void OnBarCellHeightChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Braille = s.Braille with { BarCellHeight = value } });
    }

    partial void OnBarFontSizeChanged(double value)
    {
        if (_suppressUpdates) return;
        _settings.Update(s => s with { Braille = s.Braille with { BarFontSize = value } });
    }
}
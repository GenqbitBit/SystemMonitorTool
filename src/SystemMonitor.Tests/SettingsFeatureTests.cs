using System;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.ViewModels;
using Xunit;

namespace SystemMonitor.Tests;

public sealed class SettingsFeatureTests
{
    [Fact]
    public void Defaults_ContainTheSupportedApplicationSettings()
    {
        var defaults = AppSettings.Defaults;

        Assert.Equal(900, defaults.TickIntervalMs);
        Assert.Equal(GraphStyle.Block, defaults.GraphStyle);
        Assert.Equal(4, defaults.BandCount);
        Assert.Equal(5, defaults.Block.AreaBlockWidth);
        Assert.Equal(1, defaults.Block.AreaBlockGap);
        Assert.Equal(2, defaults.Block.BarBlockWidth);
        Assert.Equal(1, defaults.Block.BarBlockGap);
        Assert.Equal(20, defaults.Braille.AreaCellWidth);
        Assert.Equal(10, defaults.Braille.AreaCellHeight);
        Assert.Equal(20, defaults.Braille.AreaFontSize);
        Assert.Equal(15, defaults.Braille.BarCellWidth);
        Assert.Equal(10, defaults.Braille.BarCellHeight);
        Assert.Equal(20, defaults.Braille.BarFontSize);
    }

    [Fact]
    public void ResetToDefaults_PreservesGraphStyleAndResetsOtherSettings()
    {
        var customSettings = new AppSettings
        {
            TickIntervalMs = 5000,
            GraphStyle = GraphStyle.Braille,
            BandCount = 20,
            Block = new BlockGraphSettings
            {
                AreaBlockWidth = 20,
                AreaBlockGap = 10,
                BarBlockWidth = 20,
                BarBlockGap = 10
            },
            Braille = new BrailleGraphSettings
            {
                AreaCellWidth = 40,
                AreaCellHeight = 30,
                AreaFontSize = 36,
                BarCellWidth = 40,
                BarCellHeight = 30,
                BarFontSize = 36
            }
        };
        var service = new InMemorySettingsService(customSettings);
        var viewModel = new SettingsPanelViewModel(service);

        viewModel.ResetToDefaultsCommand.Execute(null);

        var defaults = AppSettings.Defaults;
        // GraphStyle should be preserved
        Assert.Equal(GraphStyle.Braille, viewModel.GraphStyle);
        // All other settings should be reset to defaults
        Assert.Equal(defaults.TickIntervalMs, viewModel.TickIntervalMs);
        Assert.Equal(defaults.BandCount, viewModel.BandCount);
        Assert.Equal(defaults.Block.AreaBlockWidth, viewModel.AreaBlockWidth);
        Assert.Equal(defaults.Block.AreaBlockGap, viewModel.AreaBlockGap);
        Assert.Equal(defaults.Block.BarBlockWidth, viewModel.BarBlockWidth);
        Assert.Equal(defaults.Block.BarBlockGap, viewModel.BarBlockGap);
        Assert.Equal(defaults.Braille.AreaCellWidth, viewModel.AreaCellWidth);
        Assert.Equal(defaults.Braille.AreaCellHeight, viewModel.AreaCellHeight);
        Assert.Equal(defaults.Braille.AreaFontSize, viewModel.AreaFontSize);
        Assert.Equal(defaults.Braille.BarCellWidth, viewModel.BarCellWidth);
        Assert.Equal(defaults.Braille.BarCellHeight, viewModel.BarCellHeight);
        Assert.Equal(defaults.Braille.BarFontSize, viewModel.BarFontSize);
        // Service current should also preserve GraphStyle
        Assert.Equal(GraphStyle.Braille, service.Current.GraphStyle);
        Assert.Equal(1, service.ResetCount);
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        public InMemorySettingsService(AppSettings current) => Current = current;

        public AppSettings Current { get; private set; }

        public event Action<AppSettings>? SettingsApplied;

        public int ResetCount { get; private set; }

        public void Update(Func<AppSettings, AppSettings> mutate)
        {
            Current = mutate(Current);
            SettingsApplied?.Invoke(Current);
        }

        public void ResetToDefaults()
        {
            ResetCount++;
            // Preserve the current GraphStyle selection while resetting all other settings
            var currentGraphStyle = Current.GraphStyle;
            Update(current =>
            {
                var defaults = AppSettings.Defaults;
                return defaults with { GraphStyle = currentGraphStyle };
            });
        }
    }
}

using System;
using System.IO;
using System.Linq;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Infrastructure.Persistence;
using SystemMonitor.Presentation.Services;
using SystemMonitor.Presentation.ViewModels;
using Xunit;

namespace SystemMonitor.Tests;

public sealed class PanelWindowFeatureTests
{
    [Fact]
    public void SetPanelState_ActivatesOnlyTheRequestedPanelAndCanClearIt()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"system-monitor-panel-tests-{Guid.NewGuid():N}.json");
        var vm = new MainWindowViewModel(
            new JsonSettingsService(settingsPath),
            new MetricHistoryStore());

        vm.SetPanelNavState(PanelWindowService.PanelLabels.ViewDataTable, true);

        Assert.True(vm.NavItems.Single(item => item.Label == PanelWindowService.PanelLabels.ViewDataTable).IsActive);
        Assert.All(
            vm.NavItems.Where(item => item.Label != PanelWindowService.PanelLabels.ViewDataTable),
            item => Assert.False(item.IsActive));

        vm.SetPanelNavState(PanelWindowService.PanelLabels.ViewDataTable, false);

        Assert.All(vm.NavItems, item => Assert.False(item.IsActive));
    }
}

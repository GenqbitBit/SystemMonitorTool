using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;

namespace SystemMonitor.Presentation.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IMetricsSnapshotProvider _snapshotProvider;
    private readonly DispatcherTimer _timer;

    // The dashboard data table's rows — rebuilt wholesale every tick,
    // mirroring MainWindowViewModel's Metrics river pattern.
    [ObservableProperty]
    private ObservableCollection<MetricTableRow> rows = new();

    public DashboardViewModel()
        : this(new CatalogDesignTimeMetricsSnapshotProvider())
    {
    }

    public DashboardViewModel(IMetricsSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
        Refresh();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _timer.Tick += (_, _) => Refresh();
        if (!Avalonia.Controls.Design.IsDesignMode)
            _timer.Start();
    }

    private void Refresh()
    {
        Rows = new ObservableCollection<MetricTableRow>(
            _snapshotProvider.GetSnapshot()
                .Where(r => r.IsAvailable)
                .Select(MetricTableRow.From));
    }
}
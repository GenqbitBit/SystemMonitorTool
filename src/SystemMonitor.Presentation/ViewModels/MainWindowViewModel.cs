using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMetricsSnapshotProvider _metricsProvider;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private ObservableCollection<MetricReading> metrics = new();

    // Design-time only — used by the XAML previewer, never by the real running app
    public MainWindowViewModel()
        : this(new CatalogDesignTimeMetricsSnapshotProvider())
        {
        }

    public MainWindowViewModel(IMetricsSnapshotProvider metricsProvider)
    {
        _metricsProvider = metricsProvider;

        RefreshMetrics();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
    }

    private void RefreshMetrics()
    {
        Metrics = new ObservableCollection<MetricReading>(_metricsProvider.GetSnapshot());
    }
}
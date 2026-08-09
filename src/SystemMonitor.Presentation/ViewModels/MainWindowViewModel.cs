using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Application.UseCases;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMetricsSnapshotProvider _metricsProvider;
    private readonly IMetricHistoryStore _historyStore;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private ObservableCollection<MetricReading> metrics = new();

    [ObservableProperty]
    private string? dedicatedGpuMetricId;

    [ObservableProperty]
    private string? integratedGpuMetricId;

    public IMetricHistoryStore HistoryStore => _historyStore;

    // Design-time only — used by the XAML previewer, never by the real running app
    public MainWindowViewModel()
        : this(new CatalogDesignTimeMetricsSnapshotProvider(), new MetricHistoryStore())
        {
        }

    public MainWindowViewModel(IMetricsSnapshotProvider metricsProvider, IMetricHistoryStore historyStore)
    {
        _metricsProvider = metricsProvider;
        _historyStore = historyStore;

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
        var snapshot = _metricsProvider.GetSnapshot();
        Metrics = new ObservableCollection<MetricReading>(snapshot);
        _historyStore.Record(snapshot);

        DedicatedGpuMetricId = snapshot.FirstOrDefault(m =>
            m.Id.StartsWith("gpu.usage.") && m.GpuIsIntegrated == false)?.Id;
        IntegratedGpuMetricId = snapshot.FirstOrDefault(m =>
            m.Id.StartsWith("gpu.usage.") && m.GpuIsIntegrated == true)?.Id;
    }

    private static string? FindGpuMetricId(IReadOnlyList<MetricReading> snapshot, bool isIntegrated)
    {
        // Match on the label suffix MetricsSnapshotProvider bakes in
        // ("... - Dedicated: ..." / "... - Integrated: ...") since MetricReading
        // itself doesn't carry a raw IsIntegrated flag — only GpuInfo does, and
        // that's not part of the snapshot's public shape.
        var tag = isIntegrated ? "Integrated" : "Dedicated";
        return snapshot.FirstOrDefault(m =>
            m.Id.StartsWith("gpu.usage.") && m.Label.Contains($"- {tag}:"))?.Id;
    }

}
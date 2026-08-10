using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMetricsSnapshotProvider _metricsProvider;
    private readonly IMetricHistoryStore _historyStore;
    private readonly IOsMonitorService _os;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private ObservableCollection<MetricReading> metrics = new();

    [ObservableProperty]
    private string? dedicatedGpuMetricId;

    [ObservableProperty]
    private string? integratedGpuMetricId;

    // One entry per physical GPU detected this snapshot, keyed on the real
    // DeviceId — drives the dynamic per-GPU panel ItemsControl in MainWindow.axaml.
    [ObservableProperty]
    private ObservableCollection<GpuDeviceDisplayInfo> detectedGpus = new();

    // The heaviest processes right now — the OS section's live table
    // (the parallel tabular path, beside the scalar Metrics river).
    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topProcesses = new();

    public IMetricHistoryStore HistoryStore => _historyStore;

    public MainWindowViewModel()
        : this(new CatalogDesignTimeMetricsSnapshotProvider(), new MetricHistoryStore(), new DotNetOsMonitorService())
    {
    }

   public MainWindowViewModel(IMetricsSnapshotProvider metricsProvider, IMetricHistoryStore historyStore, IOsMonitorService os)
    {
        _metricsProvider = metricsProvider;
        _historyStore = historyStore;
        _os = os;
        RefreshMetrics();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _timer.Tick += (_, _) => RefreshMetrics();

        if (!Avalonia.Controls.Design.IsDesignMode)
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
        DetectedGpus = new ObservableCollection<GpuDeviceDisplayInfo>(
            snapshot
                .Where(m => m.Id.StartsWith("gpu.usage.") && m.GpuDeviceId != null)
                .Select(m => new GpuDeviceDisplayInfo(
                    m.GpuDeviceId!,
                    m.GpuIndex ?? 0,
                    m.GpuIsIntegrated ?? false,
                    $"GPU {m.GpuIndex} ({(m.GpuIsIntegrated == true ? "Integrated" : "Dedicated")})"))
                .DistinctBy(g => g.DeviceId)
                .OrderBy(g => g.Index));

        // Second consumer of the singleton OS service. Safe by design:
        // CpuPercent is computed against real elapsed time between calls,
        // not against tick counts, so two callers can't corrupt the rates.
        TopProcesses = new ObservableCollection<ProcessInfo>(_os.GetCurrentInfo().TopProcesses);
    }

    private static string? FindGpuMetricId(IReadOnlyList<MetricReading> snapshot, bool isIntegrated)
    {
        var tag = isIntegrated ? "Integrated" : "Dedicated";
        return snapshot.FirstOrDefault(m =>
            m.Id.StartsWith("gpu.usage.") && m.Label.Contains($"- {tag}:"))?.Id;
    }
}
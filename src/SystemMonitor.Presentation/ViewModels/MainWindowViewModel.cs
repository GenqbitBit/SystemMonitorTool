using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;
using SystemMonitor.Infrastructure.Persistence;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IMetricsSnapshotProvider _metricsProvider;
    private readonly IMetricHistoryStore _historyStore;
    private readonly IMetricHistoryPersistenceService _historyPersistence;
    private readonly IOsMonitorService _os;

    // A single dedicated background thread drives polling instead of
    // DispatcherTimer.Tick (which ran the expensive work on the UI thread)
    // or Task.Run per tick (which hands the work to a *different* ThreadPool
    // thread every cycle). Hardware-access libraries such as
    // LibreHardwareMonitorLib can behave inconsistently when called from a
    // different thread each time; a dedicated thread gives GetSnapshot() the
    // same consistent calling thread every cycle, matching how the original
    // DispatcherTimer.Tick always ran on the same (UI) thread — just moved
    // off it.
    private readonly Thread? _pollingThread;
    private readonly CancellationTokenSource _pollingCts = new();

    [ObservableProperty]
    private ObservableCollection<MetricReading> metrics = new();

    [ObservableProperty]
    private string? dedicatedGpuMetricId;

    [ObservableProperty]
    private string? integratedGpuMetricId;

    [ObservableProperty]
    private string? dedicatedGpuModelId;

    [ObservableProperty]
    private string? integratedGpuModelId;

    [ObservableProperty]
    private double _responsiveScale = 1.0;

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
    : this(new CatalogDesignTimeMetricsSnapshotProvider(), new MetricHistoryStore(),
           new DotNetOsMonitorService(), new SqliteMetricHistoryPersistenceService(),
           new MetricsTableViewModel(new CatalogDesignTimeMetricsSnapshotProvider()))
    {
    }

    public MainWindowViewModel(
        IMetricsSnapshotProvider metricsProvider,
        IMetricHistoryStore historyStore,
        IOsMonitorService os,
        IMetricHistoryPersistenceService historyPersistence,
        MetricsTableViewModel metricsTable)
    {
        _metricsProvider = metricsProvider;
        _historyStore = historyStore;
        _os = os;
        _historyPersistence = historyPersistence;
        MetricsTable = metricsTable;

        // One synchronous acquisition up front so the designer and the first
        // frame have data immediately, matching the original constructor's
        // behavior of calling RefreshMetrics() before the timer starts.
        AcquireAndApply();

        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            _pollingThread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "MetricsPolling"
            };
            // WMI/COM-based hardware access can require an STA thread. The UI
            // thread that ran the first AcquireAndApply() call above is STA
            // by convention on Windows; this dedicated thread defaults to MTA
            // unless told otherwise, which can make every call after the
            // first one silently fail for COM-based providers.
            if (OperatingSystem.IsWindows())
                _pollingThread.SetApartmentState(ApartmentState.STA);
            _pollingThread.Start();
        }
    }

    // This table's view-model.
    public MetricsTableViewModel MetricsTable { get; }

    private void PollLoop()
    {
        var stopwatch = new System.Diagnostics.Stopwatch();
        var interval = TimeSpan.FromMilliseconds(700);

        while (!_pollingCts.IsCancellationRequested)
        {
            stopwatch.Restart();

            try
            {
                AcquireAndApply();
            }
            catch
            {
                // A single failed acquisition shouldn't kill the polling loop —
                // just skip this tick and try again next cycle.
            }

            var remaining = interval - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining);
        }
    }

    private void AcquireAndApply()
    {
        var snapshot = _metricsProvider.GetSnapshot();

        _historyStore.Record(snapshot);
        _historyPersistence.Record(snapshot);

        var gpuUsageRows = snapshot
            .Where(m => m.Id.StartsWith("gpu.usage.") && m.GpuDeviceId != null)
            .ToList();

        var dedicatedId = gpuUsageRows.FirstOrDefault(m =>
            m.GpuIsIntegrated == false)?.Id
            ?? gpuUsageRows.FirstOrDefault()?.Id;
        var integratedId = gpuUsageRows.FirstOrDefault(m =>
            m.GpuIsIntegrated == true)?.Id
            ?? gpuUsageRows.FirstOrDefault()?.Id;

        var gpuModelRows = snapshot
            .Where(m => m.Id.StartsWith("gpu.model.") && m.GpuDeviceId != null)
            .ToList();

        var dedicatedModelId = gpuModelRows.FirstOrDefault(m =>
            m.GpuIsIntegrated == false)?.Id
            ?? gpuModelRows.FirstOrDefault()?.Id;
        var integratedModelId = gpuModelRows.FirstOrDefault(m =>
            m.GpuIsIntegrated == true)?.Id
            ?? gpuModelRows.FirstOrDefault()?.Id;

        var gpus = gpuUsageRows
            .Select(m => new GpuDeviceDisplayInfo(
                m.GpuDeviceId!,
                m.GpuIndex ?? 0,
                m.GpuIsIntegrated ?? false,
                $"GPU {m.GpuIndex} ({(m.GpuIsIntegrated == true ? "Integrated" : "Dedicated")})"))
            .DistinctBy(g => g.DeviceId)
            .OrderBy(g => g.Index)
            .ToList();

        var processes = _os.GetCurrentInfo().TopProcesses;

        void Apply()
        {
            Metrics.SyncFrom(snapshot, m => m.Id);
            DedicatedGpuMetricId = dedicatedId;
            IntegratedGpuMetricId = integratedId;
            DedicatedGpuModelId = dedicatedModelId;
            IntegratedGpuModelId = integratedModelId;
            DetectedGpus.SyncFrom(gpus, g => g.DeviceId);
            TopProcesses.SyncFrom(processes, p => p.ProcessId);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }


    public void UpdateResponsiveScale(double windowWidth, double windowHeight)
    {
        const double designWidth = 850;
        double raw = windowWidth / designWidth;

        // dampen further: only apply a third of the proportional growth beyond 1.0
        double dampened = 1.0 + (raw - 1.0) * 0.25;

        ResponsiveScale = Math.Clamp(dampened, 0.9, 1.2);
    }

    /// <summary>
    /// Signals the polling thread to stop and waits briefly for it to exit.
    /// Call this on window close/app shutdown so the background thread doesn't
    /// keep running (and keep polling hardware) past the window's lifetime.
    /// </summary>
    public void Dispose()
    {
        _pollingCts.Cancel();
        _pollingThread?.Join(TimeSpan.FromSeconds(2));
        _pollingCts.Dispose();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Infrastructure.Monitoring.CrossPlatform;
using SystemMonitor.Infrastructure.Persistence;
using SystemMonitor.Presentation.Common;
using SystemMonitor.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private sealed class DesignTimeDependencies
    {
        public IMetricsSnapshotProvider MetricsProvider { get; } = new CatalogDesignTimeMetricsSnapshotProvider();
        public IMetricHistoryStore HistoryStore { get; } = new MetricHistoryStore();
        public IOsMonitorService Os { get; } = new DotNetOsMonitorService();
        public IMetricHistoryPersistenceService HistoryPersistence { get; } = new SqliteMetricHistoryPersistenceService();
        public IEventLogService EventLog { get; } = new SqliteEventLogService();
        public MetricsTableViewModel MetricsTable { get; } = new MetricsTableViewModel();
        public IHardwareTreeProvider HardwareTreeProvider { get; } = new DesignTimeHardwareTreeProvider();
        public ISettingsService Settings { get; } = new JsonSettingsService(
            Path.Combine(AppContext.BaseDirectory, "settings.json"));
    }

    private IMetricsSnapshotProvider? _metricsProvider;
    private readonly IMetricHistoryStore _historyStore;
    private readonly HardwareTreeViewModel _hardwareTree;
    private IMetricHistoryPersistenceService? _historyPersistence;
    private IEventLogService? _eventLog;
    private IThresholdMonitorService? _thresholdMonitor;
    private IOsMonitorService? _os;
    private AlertMonitor? _alertMonitor;
    private volatile IReadOnlyList<MetricReading> _latestSnapshot = Array.Empty<MetricReading>();

    // Reused when the GPU identity set is unchanged, so the polling thread does
    // not copy the collection (and re-run GPU LINQ) on every tick.
    private GpuDeviceDisplayInfo[] _lastGpuDevices = Array.Empty<GpuDeviceDisplayInfo>();
    private readonly object _uiUpdateGate = new();
    private Action? _pendingUiUpdate;
    private bool _uiUpdateQueued;
    private bool _disposed;
    private readonly ISettingsService _settings;
    private volatile int _tickIntervalMs;

    private Thread? _pollingThread;
    private readonly CancellationTokenSource _pollingCts = new();

    [ObservableProperty]
    private ObservableCollection<MetricReading> metrics = new();

    [ObservableProperty]
    private int tickIntervalMs;

    [ObservableProperty]
    private string? dedicatedGpuMetricId;

    [ObservableProperty]
    private string? integratedGpuMetricId;

    [ObservableProperty]
    private string? dedicatedGpuModelId;

    [ObservableProperty]
    private string? integratedGpuModelId;

    [ObservableProperty]
    private bool hasAvailableGpu;

    [ObservableProperty]
    private ObservableCollection<GpuDeviceDisplayInfo> detectedGpus = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topProcesses = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topCpuProcesses = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topMemoryProcesses = new();

    public IMetricHistoryStore HistoryStore => _historyStore;

    public ISettingsService Settings => _settings;

    public HardwareTreeViewModel HardwareTree => _hardwareTree;

    public IReadOnlyList<MetricReading> LatestSnapshot => _latestSnapshot;

    public void SetAlertMonitor(AlertMonitor alertMonitor)
    {
        _alertMonitor = alertMonitor;
    }

    private sealed class DesignTimeHardwareTreeProvider : IHardwareTreeProvider
    {
        public IReadOnlyList<HardwareTreeNode> DiscoverTree() =>
            Array.Empty<HardwareTreeNode>();

        public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
        {
        }
    }

    public MainWindowViewModel()
        : this(new DesignTimeDependencies())
    {
    }

    public MainWindowViewModel(ISettingsService settings, IMetricHistoryStore historyStore)
    {
        _settings = settings;
        _historyStore = historyStore;
        _hardwareTree = new HardwareTreeViewModel();
        MetricsTable = new MetricsTableViewModel();
        TemperatureTable = new MetricsTableViewModel(
            new CatalogDesignTimeMetricsSnapshotProvider(),
            "Temperature Table",
            MetricKind.Temperature);
        LogsPanel = new LogsPanelViewModel();
        InitializePresentationState();
    }

    private MainWindowViewModel(DesignTimeDependencies dependencies)
        : this(
            dependencies.MetricsProvider,
            dependencies.HistoryStore,
            dependencies.Os,
            dependencies.HistoryPersistence,
            dependencies.EventLog,
            new ThresholdMonitorService(dependencies.EventLog, Array.Empty<MetricThreshold>()),
            dependencies.MetricsTable,
            dependencies.HardwareTreeProvider,
            dependencies.Settings)
    {
    }

    public MainWindowViewModel(
        IMetricsSnapshotProvider metricsProvider,
        IMetricHistoryStore historyStore,
        IOsMonitorService os,
        IMetricHistoryPersistenceService historyPersistence,
        IEventLogService eventLog,
        IThresholdMonitorService thresholdMonitor,
        MetricsTableViewModel metricsTable,
        IHardwareTreeProvider hardwareTreeProvider,
        ISettingsService settings)
    {
        _metricsProvider = metricsProvider;
        _historyStore = historyStore;
        _os = os;
        _historyPersistence = historyPersistence;
        _eventLog = eventLog;
        _thresholdMonitor = thresholdMonitor;
        _settings = settings;

        InitializePresentationState();
        MetricsTable = metricsTable;
        TemperatureTable = new MetricsTableViewModel(
            metricsProvider,
            "Temperature Table",
            MetricKind.Temperature);
        LogsPanel = new LogsPanelViewModel(eventLog);
        _hardwareTree = new HardwareTreeViewModel(hardwareTreeProvider, eventLog);

        StartMetricsPolling();
    }

    private void InitializePresentationState()
    {

        _tickIntervalMs = _settings.Current.TickIntervalMs;
        TickIntervalMs = _tickIntervalMs;
        _settings.SettingsApplied += OnSettingsApplied;

        NavItems = new ObservableCollection<NavItemViewModel>
        {
            new(PanelWindowService.PanelLabels.Settings, SelectNavItem),
            new(PanelWindowService.PanelLabels.Themes, SelectNavItem),
            new(PanelWindowService.PanelLabels.Logs, SelectNavItem),
            new(PanelWindowService.PanelLabels.ViewTopProcesses, SelectNavItem),
            new(PanelWindowService.PanelLabels.ViewDataTable, SelectNavItem),
            new(PanelWindowService.PanelLabels.ViewTemperatures, SelectNavItem),
        };

    }

    public MetricsTableViewModel MetricsTable { get; private set; } = null!;

    public MetricsTableViewModel TemperatureTable { get; }

    public LogsPanelViewModel LogsPanel { get; private set; } = null!;

    public ObservableCollection<NavItemViewModel> NavItems { get; private set; } = null!;

    public void SetPanelNavState(string label, bool isActive)
    {
        foreach (var item in NavItems)
            item.IsActive = isActive && item.Label == label;
    }

    private void SelectNavItem(NavItemViewModel selected)
    {
        foreach (var item in NavItems)
            item.IsActive = item == selected;

        OpenPanelRequested?.Invoke(selected.Label);
    }

    public event Action<string>? OpenPanelRequested;

    public sealed record BackendServices(
        IMetricsSnapshotProvider MetricsProvider,
        IMetricHistoryPersistenceService HistoryPersistence,
        IEventLogService EventLog,
        IThresholdMonitorService ThresholdMonitor,
        IOsMonitorService Os,
        IHardwareTreeProvider HardwareTreeProvider);

    private sealed record CoreBackendServices(
        IMetricsSnapshotProvider MetricsProvider,
        IOsMonitorService Os);

    public static BackendServices ResolveBackendServices(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IMetricsSnapshotProvider>(),
            provider.GetRequiredService<IMetricHistoryPersistenceService>(),
            provider.GetRequiredService<IEventLogService>(),
            provider.GetRequiredService<IThresholdMonitorService>(),
            provider.GetRequiredService<IOsMonitorService>(),
            provider.GetRequiredService<IHardwareTreeProvider>());

    private static CoreBackendServices ResolveCoreBackendServices(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IMetricsSnapshotProvider>(),
            provider.GetRequiredService<IOsMonitorService>());

    public void AttachBackend(BackendServices backend)
    {
        if (_disposed)
            return;

        _metricsProvider = backend.MetricsProvider;
        _historyPersistence = backend.HistoryPersistence;
        _eventLog = backend.EventLog;
        _thresholdMonitor = backend.ThresholdMonitor;
        _os = backend.Os;

        LogsPanel.Attach(backend.EventLog);
        _hardwareTree.Start(backend.HardwareTreeProvider, backend.EventLog);
        StartMetricsPolling();
    }

    private void StartMetricsPolling()
    {
        if (_pollingThread is not null || Avalonia.Controls.Design.IsDesignMode)
            return;

        _pollingThread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "MetricsPolling"
        };

        if (OperatingSystem.IsWindows())
            _pollingThread.SetApartmentState(ApartmentState.STA);

        _eventLog?.LogEvent(
            EventType.SessionStart,
            "Application session started");

        _pollingThread.Start();
    }

    public async Task InitializeBackendAsync(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var core = await Task.Run(
                    () => ResolveCoreBackendServices(provider),
                    cancellationToken)
                .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(
                () => AttachCoreBackend(core),
                DispatcherPriority.Normal,
                cancellationToken);

            var auxiliary = await Task.Run(
                    () => ResolveBackendServices(provider),
                    cancellationToken)
                .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(
                () => AttachAuxiliaryBackend(auxiliary),
                DispatcherPriority.Normal,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Startup initialization failed: {ex}");
        }
    }

    private void AttachCoreBackend(CoreBackendServices backend)
    {
        if (_disposed)
            return;

        _metricsProvider = backend.MetricsProvider;
        _os = backend.Os;
        StartMetricsPolling();
    }

    private void AttachAuxiliaryBackend(BackendServices backend)
    {
        if (_disposed)
            return;

        _historyPersistence = backend.HistoryPersistence;
        _eventLog = backend.EventLog;
        _thresholdMonitor = backend.ThresholdMonitor;

        LogsPanel.Attach(backend.EventLog);
        _hardwareTree.Start(backend.HardwareTreeProvider, backend.EventLog);
    }

    private void PollLoop()
    {
        var stopwatch = new System.Diagnostics.Stopwatch();

        while (!_pollingCts.IsCancellationRequested)
        {
            stopwatch.Restart();

            try
            {
                AcquireAndApply();
            }
            catch (Exception ex)
            {
                _eventLog?.LogEvent(EventType.Error, ex.Message);
            }

            var interval = TimeSpan.FromMilliseconds(Math.Max(100, _tickIntervalMs));
            var remaining = interval - stopwatch.Elapsed;

            // Wait on the cancellation handle rather than Thread.Sleep so a
            // shutdown does not have to wait out the current tick interval.
            if (remaining > TimeSpan.Zero)
                _pollingCts.Token.WaitHandle.WaitOne(remaining);
        }
    }

    private void OnSettingsApplied(AppSettings settings)
    {
        _tickIntervalMs = settings.TickIntervalMs;

        if (Dispatcher.UIThread.CheckAccess())
            TickIntervalMs = settings.TickIntervalMs;
        else
            Dispatcher.UIThread.Post(() => TickIntervalMs = settings.TickIntervalMs);
    }

    private void AcquireAndApply()
    {
        var metricsProvider = _metricsProvider;
        var historyPersistence = _historyPersistence;
        var thresholdMonitor = _thresholdMonitor;
        var os = _os;
        if (metricsProvider is null || os is null)
            return;

        var previousSnapshot = _latestSnapshot;
        var previousDedicatedGpuMetricId = DedicatedGpuMetricId;
        var previousIntegratedGpuMetricId = IntegratedGpuMetricId;
        var previousDedicatedGpuModelId = DedicatedGpuModelId;
        var previousIntegratedGpuModelId = IntegratedGpuModelId;
        var previousHasAvailableGpu = HasAvailableGpu;

        var snapshot = metricsProvider.GetSnapshot();
        _latestSnapshot = snapshot;

        _historyStore.Record(snapshot);
        historyPersistence?.Record(snapshot);
        thresholdMonitor?.Check(snapshot);
        _alertMonitor?.Check(snapshot);

        var metricsChanged = !AreMetricsEquivalent(previousSnapshot, snapshot);

        string? dedicatedId = null;
        string? integratedId = null;
        string? dedicatedModelId = null;
        string? integratedModelId = null;
        var gpus = Array.Empty<GpuDeviceDisplayInfo>();

        if (metricsChanged || previousSnapshot.Count == 0)
        {
            // One pass over the snapshot instead of three Where(...).ToList()
            // sequences plus DistinctBy/OrderBy rebuilds — this runs on the
            // polling thread every tick while values are changing.
            string? firstUsageId = null;
            string? firstModelId = null;
            HashSet<string>? seenDeviceIds = null;
            List<GpuDeviceDisplayInfo>? gpuList = null;

            for (var i = 0; i < snapshot.Count; i++)
            {
                var reading = snapshot[i];
                var deviceId = reading.GpuDeviceId;
                if (deviceId is null)
                    continue;

                if (reading.Id.StartsWith("gpu.usage.", StringComparison.Ordinal))
                {
                    if (!reading.IsAvailable)
                        continue;

                    firstUsageId ??= reading.Id;
                    if (reading.GpuIsIntegrated == false)
                        dedicatedId ??= reading.Id;
                    else if (reading.GpuIsIntegrated == true)
                        integratedId ??= reading.Id;

                    seenDeviceIds ??= new HashSet<string>(StringComparer.Ordinal);
                    if (seenDeviceIds.Add(deviceId))
                    {
                        gpuList ??= new List<GpuDeviceDisplayInfo>(2);
                        gpuList.Add(new GpuDeviceDisplayInfo(
                            deviceId,
                            reading.GpuIndex ?? 0,
                            reading.GpuIsIntegrated ?? false,
                            $"GPU {reading.GpuIndex} ({(reading.GpuIsIntegrated == true ? "Integrated" : "Dedicated")})"));
                    }
                }
                else if (reading.Id.StartsWith("gpu.model.", StringComparison.Ordinal))
                {
                    firstModelId ??= reading.Id;
                    if (reading.GpuIsIntegrated == false)
                        dedicatedModelId ??= reading.Id;
                    else if (reading.GpuIsIntegrated == true)
                        integratedModelId ??= reading.Id;
                }
            }

            // Preserve the previous single-GPU fallbacks.
            dedicatedId ??= firstUsageId;
            integratedId ??= firstUsageId;
            dedicatedModelId ??= firstModelId;
            integratedModelId ??= firstModelId;

            if (gpuList is null)
            {
                gpus = Array.Empty<GpuDeviceDisplayInfo>();
            }
            else
            {
                gpuList.Sort(static (a, b) => a.Index.CompareTo(b.Index));
                gpus = gpuList.ToArray();
            }
        }
        else
        {
            dedicatedId = previousDedicatedGpuMetricId;
            integratedId = previousIntegratedGpuMetricId;
            dedicatedModelId = previousDedicatedGpuModelId;
            integratedModelId = previousIntegratedGpuModelId;
            gpus = _lastGpuDevices;
        }

        _lastGpuDevices = gpus;

        var osInfo = os.LastInfo ?? os.GetCurrentInfo();

        var combinedProcesses = osInfo.TopProcesses;
        var cpuProcesses = osInfo.TopProcessesByCpu;
        var memoryProcesses = osInfo.TopProcessesByMemory;

        var shouldApplyUi = metricsChanged
            || !string.Equals(previousDedicatedGpuMetricId, dedicatedId, StringComparison.Ordinal)
            || !string.Equals(previousIntegratedGpuMetricId, integratedId, StringComparison.Ordinal)
            || !string.Equals(previousDedicatedGpuModelId, dedicatedModelId, StringComparison.Ordinal)
            || !string.Equals(previousIntegratedGpuModelId, integratedModelId, StringComparison.Ordinal)
            || previousHasAvailableGpu != (gpus.Length > 0)
            || !DetectedGpus.SequenceEqual(gpus)
            || !TopProcesses.SequenceEqual(combinedProcesses)
            || !TopCpuProcesses.SequenceEqual(cpuProcesses)
            || !TopMemoryProcesses.SequenceEqual(memoryProcesses);

        if (!shouldApplyUi)
            return;

        void Apply()
        {
            Metrics.SyncFrom(snapshot, m => m.Id);
            MetricsTable.ApplySnapshot(snapshot);
            TemperatureTable.ApplySnapshot(snapshot);

            DedicatedGpuMetricId = dedicatedId;
            IntegratedGpuMetricId = integratedId;

            DedicatedGpuModelId = dedicatedModelId;
            IntegratedGpuModelId = integratedModelId;
            HasAvailableGpu = gpus.Length > 0;

            DetectedGpus.SyncFrom(
                gpus,
                g => g.DeviceId);

            TopProcesses.SyncFrom(
                combinedProcesses,
                p => p.ProcessId);

            TopCpuProcesses.SyncFrom(
                cpuProcesses,
                p => p.ProcessId);

            TopMemoryProcesses.SyncFrom(
                memoryProcesses,
                p => p.ProcessId);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            QueueUiUpdate(Apply);
    }

    private static bool AreMetricsEquivalent(IReadOnlyList<MetricReading> left, IReadOnlyList<MetricReading> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            var a = left[i];
            var b = right[i];
            if (a.Id != b.Id || a.Label != b.Label || a.Value != b.Value
                || a.IsAvailable != b.IsAvailable || a.TextValue != b.TextValue)
                return false;
        }

        return true;
    }

    private void QueueUiUpdate(Action update)
    {
        lock (_uiUpdateGate)
        {
            _pendingUiUpdate = update;
            if (_uiUpdateQueued)
                return;

            _uiUpdateQueued = true;
        }

        Dispatcher.UIThread.Post(DrainUiUpdates);
    }

    private void DrainUiUpdates()
    {
        if (_disposed)
        {
            lock (_uiUpdateGate)
            {
                _pendingUiUpdate = null;
                _uiUpdateQueued = false;
            }
            return;
        }

        while (true)
        {
            Action? update;
            lock (_uiUpdateGate)
            {
                update = _pendingUiUpdate;
                _pendingUiUpdate = null;
                if (update is null)
                {
                    _uiUpdateQueued = false;
                    return;
                }
            }

            update();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _settings.SettingsApplied -= OnSettingsApplied;
        lock (_uiUpdateGate)
            _pendingUiUpdate = null;
        _pollingCts.Cancel();

        // Bounded wait: a wedged hardware driver must not make the app
        // unclosable. The token is only disposed when the thread exited;
        // disposing it under a still-running loop would throw inside the
        // background thread.
        var pollingStopped = _pollingThread is null
            || _pollingThread.Join(TimeSpan.FromSeconds(3));

        if (pollingStopped)
            _pollingCts.Dispose();

        MetricsTable.Dispose();
        TemperatureTable.Dispose();
        LogsPanel.Dispose();
        _hardwareTree.Dispose();

        (_historyPersistence as IDisposable)?.Dispose();
        (_eventLog as IDisposable)?.Dispose();
    }
}
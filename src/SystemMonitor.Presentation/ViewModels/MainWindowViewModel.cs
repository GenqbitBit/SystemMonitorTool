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
    private IReadOnlyList<MetricReading> _latestSnapshot = Array.Empty<MetricReading>();
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
        };

    }

    public MetricsTableViewModel MetricsTable { get; private set; } = null!;

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

            var interval = TimeSpan.FromMilliseconds(_tickIntervalMs);
            var remaining = interval - stopwatch.Elapsed;

            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining);
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

        var metricsChanged = !AreMetricsEquivalent(previousSnapshot, snapshot);

        string? dedicatedId = null;
        string? integratedId = null;
        string? dedicatedModelId = null;
        string? integratedModelId = null;
        var gpus = Array.Empty<GpuDeviceDisplayInfo>();

        if (metricsChanged || previousSnapshot.Count == 0)
        {
            var gpuUsageRows = snapshot
                .Where(m =>
                    m.Id.StartsWith("gpu.usage.") &&
                    m.GpuDeviceId != null &&
                    m.IsAvailable)
                .ToList();

            dedicatedId = gpuUsageRows.FirstOrDefault(m => m.GpuIsIntegrated == false)?.Id
                ?? gpuUsageRows.FirstOrDefault()?.Id;

            integratedId = gpuUsageRows.FirstOrDefault(m => m.GpuIsIntegrated == true)?.Id
                ?? gpuUsageRows.FirstOrDefault()?.Id;

            var gpuModelRows = snapshot
                .Where(m =>
                    m.Id.StartsWith("gpu.model.") &&
                    m.GpuDeviceId != null)
                .ToList();

            dedicatedModelId = gpuModelRows.FirstOrDefault(m => m.GpuIsIntegrated == false)?.Id
                ?? gpuModelRows.FirstOrDefault()?.Id;

            integratedModelId = gpuModelRows.FirstOrDefault(m => m.GpuIsIntegrated == true)?.Id
                ?? gpuModelRows.FirstOrDefault()?.Id;

            gpus = gpuUsageRows
                .Select(m => new GpuDeviceDisplayInfo(
                    m.GpuDeviceId!,
                    m.GpuIndex ?? 0,
                    m.GpuIsIntegrated ?? false,
                    $"GPU {m.GpuIndex} ({(m.GpuIsIntegrated == true
                        ? "Integrated"
                        : "Dedicated")})"))
                .DistinctBy(g => g.DeviceId)
                .OrderBy(g => g.Index)
                .ToArray();
        }
        else
        {
            dedicatedId = previousDedicatedGpuMetricId;
            integratedId = previousIntegratedGpuMetricId;
            dedicatedModelId = previousDedicatedGpuModelId;
            integratedModelId = previousIntegratedGpuModelId;
            gpus = DetectedGpus.ToArray();
        }

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
            || !AreProcessListsEquivalent(DetectedGpus, gpus, g => g.DeviceId)
            || !AreProcessListsEquivalent(TopProcesses, combinedProcesses, p => p.ProcessId)
            || !AreProcessListsEquivalent(TopCpuProcesses, cpuProcesses, p => p.ProcessId)
            || !AreProcessListsEquivalent(TopMemoryProcesses, memoryProcesses, p => p.ProcessId);

        if (!shouldApplyUi)
            return;

        void Apply()
        {
            Metrics.SyncFrom(snapshot, m => m.Id);
            MetricsTable.ApplySnapshot(snapshot);

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
            if (a.Id != b.Id || a.Value != b.Value || a.IsAvailable != b.IsAvailable || a.TextValue != b.TextValue)
                return false;
        }

        return true;
    }

    private static bool AreProcessListsEquivalent<TItem, TKey>(
        ObservableCollection<TItem> current,
        IReadOnlyList<TItem> latest,
        Func<TItem, TKey> keySelector)
        where TKey : notnull
    {
        if (current.Count != latest.Count)
            return false;

        var indexByKey = new Dictionary<TKey, int>(current.Count);
        for (var i = 0; i < current.Count; i++)
            indexByKey[keySelector(current[i])] = i;

        for (var i = 0; i < latest.Count; i++)
        {
            var item = latest[i];
            var key = keySelector(item);
            if (!indexByKey.TryGetValue(key, out var currentIndex) || currentIndex != i)
                return false;

            if (!EqualityComparer<TItem>.Default.Equals(current[i], item))
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

        _pollingThread?.Join();

        _pollingCts.Dispose();

        MetricsTable.Dispose();
        LogsPanel.Dispose();
        _hardwareTree.Dispose();

        (_historyPersistence as IDisposable)?.Dispose();
        (_eventLog as IDisposable)?.Dispose();
    }
}
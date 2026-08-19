using System;
using System.Collections.Generic;
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
using SystemMonitor.Presentation.Services;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IMetricsSnapshotProvider _metricsProvider;
    private readonly IMetricHistoryStore _historyStore;
    private readonly HardwareTreeViewModel _hardwareTree;
    private readonly IMetricHistoryPersistenceService _historyPersistence;
    private readonly IEventLogService _eventLog;
    private readonly IThresholdMonitorService _thresholdMonitor;
    private readonly IOsMonitorService _os;

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

    [ObservableProperty]
    private ObservableCollection<GpuDeviceDisplayInfo> detectedGpus = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topProcesses = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topCpuProcesses = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> topMemoryProcesses = new();

    public IMetricHistoryStore HistoryStore => _historyStore;

    public HardwareTreeViewModel HardwareTree => _hardwareTree;

    private sealed class DesignTimeHardwareTreeProvider : IHardwareTreeProvider
    {
        public IReadOnlyList<HardwareTreeNode> DiscoverTree() =>
            Array.Empty<HardwareTreeNode>();

        public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
        {
        }
    }

    public MainWindowViewModel()
        : this(
            new CatalogDesignTimeMetricsSnapshotProvider(),
            new MetricHistoryStore(),
            new DotNetOsMonitorService(),
            new SqliteMetricHistoryPersistenceService(),
            new SqliteEventLogService(),
            new ThresholdMonitorService(
                new SqliteEventLogService(),
                Array.Empty<MetricThreshold>()),
            new MetricsTableViewModel(
                new CatalogDesignTimeMetricsSnapshotProvider()),
            new DesignTimeHardwareTreeProvider())
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
        IHardwareTreeProvider hardwareTreeProvider)
    {
        _metricsProvider = metricsProvider;
        _historyStore = historyStore;
        _os = os;
        _historyPersistence = historyPersistence;
        _eventLog = eventLog;
        _thresholdMonitor = thresholdMonitor;

        MetricsTable = metricsTable;
        LogsPanel = new LogsPanelViewModel(eventLog);
        _hardwareTree = new HardwareTreeViewModel(hardwareTreeProvider, eventLog);

        NavItems = new ObservableCollection<NavItemViewModel>
        {
            new("Settings", SelectNavItem),
            new("Themes", SelectNavItem),
            new("Logs", SelectNavItem),
            new("View Top Processes", SelectNavItem),
            new("View Data Table", SelectNavItem),
        };

        AcquireAndApply();

        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            _pollingThread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "MetricsPolling"
            };

            if (OperatingSystem.IsWindows())
                _pollingThread.SetApartmentState(ApartmentState.STA);

            _eventLog.LogEvent(
                EventType.SessionStart,
                "Application session started");

            _pollingThread.Start();
        }
    }

    public MetricsTableViewModel MetricsTable { get; }

    public LogsPanelViewModel LogsPanel { get; }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    private void SelectNavItem(NavItemViewModel selected)
    {
        foreach (var item in NavItems)
            item.IsActive = item == selected;

        OpenPanelRequested?.Invoke(selected.Label);
    }

    public event Action<string>? OpenPanelRequested;

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
            catch (Exception ex)
            {
                _eventLog.LogEvent(EventType.Error, ex.Message);
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
        _thresholdMonitor.Check(snapshot);

        var gpuUsageRows = snapshot
            .Where(m =>
                m.Id.StartsWith("gpu.usage.") &&
                m.GpuDeviceId != null)
            .ToList();

        var dedicatedId = gpuUsageRows.FirstOrDefault(m =>
            m.GpuIsIntegrated == false)?.Id
            ?? gpuUsageRows.FirstOrDefault()?.Id;

        var integratedId = gpuUsageRows.FirstOrDefault(m =>
            m.GpuIsIntegrated == true)?.Id
            ?? gpuUsageRows.FirstOrDefault()?.Id;

        var gpuModelRows = snapshot
            .Where(m =>
                m.Id.StartsWith("gpu.model.") &&
                m.GpuDeviceId != null)
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
                $"GPU {m.GpuIndex} ({(m.GpuIsIntegrated == true
                    ? "Integrated"
                    : "Dedicated")})"))
            .DistinctBy(g => g.DeviceId)
            .OrderBy(g => g.Index)
            .ToList();

        var osInfo = _os.GetCurrentInfo();

        var combinedProcesses = osInfo.TopProcesses;
        var cpuProcesses = osInfo.TopProcessesByCpu;
        var memoryProcesses = osInfo.TopProcessesByMemory;

        void Apply()
        {
            Metrics.SyncFrom(snapshot, m => m.Id);

            DedicatedGpuMetricId = dedicatedId;
            IntegratedGpuMetricId = integratedId;

            DedicatedGpuModelId = dedicatedModelId;
            IntegratedGpuModelId = integratedModelId;

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
            Dispatcher.UIThread.Post(Apply);
    }

    public void UpdateResponsiveScale(
        double windowWidth,
        double windowHeight)
    {
        const double designWidth = 850;

        double raw = windowWidth / designWidth;

        double dampened = 1.0 + (raw - 1.0) * 0.25;

        ResponsiveScale = Math.Clamp(
            dampened,
            0.9,
            1.2);
    }

    public void Dispose()
    {
        _pollingCts.Cancel();

        _pollingThread?.Join(
            TimeSpan.FromSeconds(2));

        _pollingCts.Dispose();

        _hardwareTree.Dispose();

        (_historyPersistence as IDisposable)?.Dispose();
        (_eventLog as IDisposable)?.Dispose();
    }
}
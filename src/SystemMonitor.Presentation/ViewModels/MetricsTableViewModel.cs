using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MetricsTableViewModel : ViewModelBase, IDisposable
{
    private readonly IMetricsSnapshotProvider _snapshotProvider;

    // Same dedicated-thread approach as MainWindowViewModel — see the
    // comment there for why this replaced DispatcherTimer/Task.Run.
    private readonly Thread? _pollingThread;
    private readonly CancellationTokenSource _pollingCts = new();

    // This table's rows — synced in place every tick rather than rebuilt
    // wholesale.
    [ObservableProperty]
    private ObservableCollection<MetricTableRow> rows = new();

    public MetricsTableViewModel()
        : this(new CatalogDesignTimeMetricsSnapshotProvider())
    {
    }

    public MetricsTableViewModel(IMetricsSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;

        AcquireAndApply();

        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            _pollingThread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "MetricsTablePolling"
            };
            if (OperatingSystem.IsWindows())
                _pollingThread.SetApartmentState(ApartmentState.STA);
            _pollingThread.Start();
        }
    }

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
        var rows = _snapshotProvider.GetSnapshot()
            .Where(r => r.IsAvailable)
            .Select(MetricTableRow.From)
            .ToList();

        // MetricTableRow has no single identity field, but Category + Metric
        // together uniquely identify a row (e.g. ("CPU", "Usage")) and are
        // stable across ticks even as Value/RawValue change.
        void Apply() => Rows.SyncFrom(rows, r => (r.Category, r.Metric));

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    /// <summary>
    /// Signals the polling thread to stop and waits briefly for it to exit.
    /// Call this when this view is torn down so the background thread
    /// doesn't keep running past its owning view's lifetime.
    /// </summary>
    public void Dispose()
    {
        _pollingCts.Cancel();
        _pollingThread?.Join(TimeSpan.FromSeconds(2));
        _pollingCts.Dispose();
    }
}
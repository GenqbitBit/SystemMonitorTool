using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MetricsTableViewModel : ViewModelBase, IDisposable
{
    private readonly IMetricsSnapshotProvider _snapshotProvider;

    private bool _isActive;
    private bool _disposed;

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
    }

    public void SetActive(bool isActive, IReadOnlyList<MetricReading>? latestSnapshot = null)
    {
        if (_disposed)
            return;

        _isActive = isActive;
        if (isActive && latestSnapshot is not null)
            ApplySnapshot(latestSnapshot);
    }

    public void ApplySnapshot(IReadOnlyList<MetricReading> snapshot)
    {
        if (_disposed || !_isActive)
            return;

        var rows = snapshot.Select(MetricTableRow.From).ToList();

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
        _disposed = true;
        _isActive = false;
    }
}
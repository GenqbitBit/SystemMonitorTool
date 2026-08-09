using System.Collections.Generic;
using System.Linq;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

/// <summary>
/// Design-time-only IMetricsSnapshotProvider used by Avalonia's live previewer.
/// Generates one MetricReading per MetricCatalog entry using each entry's
/// sample values. Never edited directly to add/remove a metric — that always
/// happens in MetricCatalog, and this provider picks it up automatically.
/// </summary>
internal class CatalogDesignTimeMetricsSnapshotProvider : IMetricsSnapshotProvider
{
    public IReadOnlyList<MetricReading> GetSnapshot()
    {
        return MetricCatalog.All.Select(entry => new MetricReading
        {
            Id = entry.Id,
            Category = entry.Category,
            Label = entry.Label,
            Kind = entry.Kind,
            Unit = entry.Unit,
            IsAvailable = true,
            Value = entry.SampleValue,
            Min = entry.SampleMin,
            Max = entry.SampleMax,
            Average = entry.SampleAverage,
            IsPrimary = entry.SampleIsPrimary
        }).ToList();
    }
}
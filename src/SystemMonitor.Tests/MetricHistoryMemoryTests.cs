using System;
using System.Linq;
using SystemMonitor.Application.UseCases;
using SystemMonitor.Domain.Models;
using Xunit;

namespace SystemMonitor.Tests;

public class MetricHistoryMemoryTests
{
    [Fact]
    public void Record_PrunesMetricIdsThatDisappearFromSnapshots()
    {
        var store = new MetricHistoryStore(TimeSpan.FromMinutes(1));

        store.Record(new[] { Reading("metric.a", 10) });
        store.Record(new[] { Reading("metric.b", 20) });

        Assert.Empty(store.GetHistory("metric.a"));
        Assert.Equal((0, 1), store.GetCommittedRange("metric.a"));
    }

    [Fact]
    public void Record_RetainsMetricIdWhenReadingIsTemporarilyUnavailable()
    {
        var store = new MetricHistoryStore(TimeSpan.FromMinutes(1));

        store.Record(new[] { Reading("metric.a", 10) });
        store.Record(new[]
        {
            new MetricReading
            {
                Id = "metric.a",
                IsAvailable = false
            }
        });

        Assert.Single(store.GetHistory("metric.a"));
    }

    private static MetricReading Reading(string id, double value) => new()
    {
        Id = id,
        IsAvailable = true,
        Value = value
    };
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Common;

/// <summary>
/// In-place ObservableCollection updates, used in place of assigning a brand-new
/// collection instance every tick.
///
/// Replacing the whole collection (e.g. `Metrics = new ObservableCollection&lt;&gt;(snapshot)`)
/// raises a single CollectionChanged Reset notification. Bound ItemsControls/DataGrids treat
/// Reset conservatively: they tear down and fully re-realize every container, re-running
/// templates and layout for every row, on the UI thread, every tick — regardless of whether
/// the underlying values changed.
///
/// SyncFrom instead walks the collection by key and only emits Move/Insert/Replace/RemoveAt
/// for entries that actually changed position or value, and skips the write entirely when a
/// value is unchanged (relying on value-type/record equality on TItem).
/// </summary>
public static class ObservableCollectionSyncExtensions
{
    /// <summary>
    /// Syncs <paramref name="target"/> to match <paramref name="latest"/>, keyed by
    /// <paramref name="keySelector"/>.
    ///
    /// Matches by key, not by position — so this is correct whether the source enumerates in
    /// a stable order every tick (the metric/GPU catalogs) or a changing one (e.g. a process
    /// list re-sorted by CPU% each call). An earlier position-only version of this method
    /// assumed stable ordering; for a re-sorted source it inserted a "new" entry every time an
    /// existing one's rank shifted instead of recognizing it moved, producing runaway
    /// duplicates. This version looks entries up by key regardless of position, so a re-ranked
    /// process is Moved in place, not duplicated.
    /// </summary>
    public static void SyncFrom<TItem, TKey>(
        this ObservableCollection<TItem> target,
        IReadOnlyList<TItem> latest,
        Func<TItem, TKey> keySelector)
        where TKey : notnull
    {
        var latestKeys = new HashSet<TKey>(latest.Select(keySelector));

        // Drop entries no longer present (e.g. a process that exited).
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!latestKeys.Contains(keySelector(target[i])))
                target.RemoveAt(i);
        }

        // Key -> current index, kept up to date as we go. List sizes here are small
        // (tens of rows), so eager rebuilds after a structural change are cheap and
        // keep this simple and correct rather than trying to patch indices by hand.
        var indexByKey = BuildIndex(target, keySelector);

        for (int i = 0; i < latest.Count; i++)
        {
            var item = latest[i];
            var key = keySelector(item);

            if (indexByKey.TryGetValue(key, out var existingIndex))
            {
                if (existingIndex != i)
                {
                    target.Move(existingIndex, i);
                    indexByKey = BuildIndex(target, keySelector);
                }

                // Same key, same position now — only write (and notify) if the value
                // actually changed. Records/structs compare by value, so an unchanged
                // reading is a no-op here instead of a PropertyChanged + visual update.
                if (!AreMeaningfullyEqual(target[i], item))
                    target[i] = item;
            }
            else
            {
                target.Insert(i, item);
                indexByKey = BuildIndex(target, keySelector);
            }
        }
    }

    private static Dictionary<TKey, int> BuildIndex<TItem, TKey>(
        ObservableCollection<TItem> target,
        Func<TItem, TKey> keySelector)
        where TKey : notnull
    {
        var map = new Dictionary<TKey, int>(target.Count);
        for (int i = 0; i < target.Count; i++)
            map[keySelector(target[i])] = i;
        return map;
    }

    private static bool AreMeaningfullyEqual<TItem>(TItem current, TItem latest)
    {
        if (current is MetricReading currentMetric && latest is MetricReading latestMetric)
        {
            return currentMetric.Id == latestMetric.Id
                && currentMetric.Category == latestMetric.Category
                && currentMetric.Label == latestMetric.Label
                && currentMetric.Kind == latestMetric.Kind
                && currentMetric.Unit == latestMetric.Unit
                && currentMetric.IsAvailable == latestMetric.IsAvailable
                && currentMetric.Value == latestMetric.Value
                && currentMetric.Min == latestMetric.Min
                && currentMetric.Max == latestMetric.Max
                && currentMetric.Average == latestMetric.Average
                && currentMetric.TextValue == latestMetric.TextValue
                && currentMetric.IsPrimary == latestMetric.IsPrimary
                && currentMetric.GpuIndex == latestMetric.GpuIndex
                && currentMetric.GpuIsIntegrated == latestMetric.GpuIsIntegrated
                && currentMetric.GpuDeviceId == latestMetric.GpuDeviceId;
        }

        return EqualityComparer<TItem>.Default.Equals(current, latest);
    }
}
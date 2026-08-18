using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite; 
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Persistence;

/// <summary>
/// Persists metric readings to a local SQLite database at
/// %LocalAppData%/SystemMonitorTool/history.db, so historical charts can
/// look back further than the in-memory store's short rolling window and
/// survive an app restart.
///
/// Design notes:
///  - Writes go through a background Channel, never directly on the
///    caller's thread. Record() just enqueues and returns immediately —
///    safe to call every 700ms from the UI's DispatcherTimer without
///    causing jank from disk I/O.
///  - A single background loop drains the channel and writes in batches
///    inside one transaction per tick, which is far cheaper than one
///    transaction per row.
///  - Retention is fixed at 24 hours: old rows are pruned periodically
///    so the database doesn't grow unbounded over a long-running session.
/// </summary>
public sealed class SqliteMetricHistoryPersistenceService : IMetricHistoryPersistenceService, IDisposable
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);

    private readonly string _connectionString;
    private readonly Channel<(string MetricId, DateTime Timestamp, double Value)> _writeQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _backgroundWriterTask;
    private DateTime _lastCleanup = DateTime.MinValue;

    public SqliteMetricHistoryPersistenceService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemMonitorTool");
        Directory.CreateDirectory(appDataFolder);

        var dbPath = Path.Combine(appDataFolder, "history.db");
        _connectionString = $"Data Source={dbPath}";

        InitializeDatabase();

        _writeQueue = Channel.CreateUnbounded<(string, DateTime, double)>();
        _backgroundWriterTask = Task.Run(BackgroundWriteLoopAsync);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // WAL mode lets reads and writes happen concurrently without
        // locking each other out — matters here since GetHistoryAsync
        // can be called while the background writer is mid-batch.
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            pragma.ExecuteNonQuery();
        }

        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS metric_history (
                metric_id TEXT NOT NULL,
                timestamp INTEGER NOT NULL,
                value REAL NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_metric_history_lookup
                ON metric_history (metric_id, timestamp);
            """;
        create.ExecuteNonQuery();
    }

    public void Record(IReadOnlyList<MetricReading> snapshot)
    {
        var now = DateTime.UtcNow;
        foreach (var reading in snapshot)
        {
            if (!reading.IsAvailable) continue;

            // TryWrite on an unbounded channel never blocks or fails under
            // normal operation — safe to call from the UI thread every tick.
            _writeQueue.Writer.TryWrite((reading.Id, now, reading.Value));
        }
    }

    private async Task BackgroundWriteLoopAsync()
    {
        var reader = _writeQueue.Reader;

        while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(_cts.Token).ConfigureAwait(false);
            using var transaction = connection.BeginTransaction();

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO metric_history (metric_id, timestamp, value) VALUES ($id, $ts, $val);";
            var idParam = insert.Parameters.Add("$id", SqliteType.Text);
            var tsParam = insert.Parameters.Add("$ts", SqliteType.Integer);
            var valParam = insert.Parameters.Add("$val", SqliteType.Real);

            // Drain everything currently queued into one batch/transaction,
            // rather than one transaction per row.
            while (reader.TryRead(out var point))
            {
                idParam.Value = point.MetricId;
                tsParam.Value = point.Timestamp.Ticks;
                valParam.Value = point.Value;
                insert.ExecuteNonQuery();
            }

            transaction.Commit();

            await MaybeCleanupAsync(connection).ConfigureAwait(false);
        }
    }

    private async Task MaybeCleanupAsync(SqliteConnection connection)
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanup < CleanupInterval) return;
        _lastCleanup = now;

        var cutoff = now - RetentionWindow;

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM metric_history WHERE timestamp < $cutoff;";
        delete.Parameters.AddWithValue("$cutoff", cutoff.Ticks);
        await delete.ExecuteNonQueryAsync(_cts.Token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MetricHistoryPoint>> GetHistoryAsync(string metricId, DateTime since)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT timestamp, value FROM metric_history
            WHERE metric_id = $id AND timestamp >= $since
            ORDER BY timestamp;
            """;
        select.Parameters.AddWithValue("$id", metricId);
        select.Parameters.AddWithValue("$since", since.Ticks);

        var results = new List<MetricHistoryPoint>();
        using var readerResult = await select.ExecuteReaderAsync().ConfigureAwait(false);
        while (await readerResult.ReadAsync().ConfigureAwait(false))
        {
            var timestamp = new DateTime(readerResult.GetInt64(0), DateTimeKind.Utc);
            var value = readerResult.GetDouble(1);
            results.Add(new MetricHistoryPoint(timestamp, value));
        }

        return results;
    }

    public void Dispose()
    {
        _writeQueue.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            _backgroundWriterTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort shutdown — don't let a slow flush block app exit.
        }
        _cts.Dispose();
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Persistence;

/// <summary>
/// Persists numeric metric readings to
/// %LocalAppData%/SystemMonitorTool/history.db.
///
/// Writes are enqueued and drained by one background loop into bounded
/// transactions. Text metrics are not stored (they always carried a
/// meaningless 0), snapshots are sampled at most every 750 ms, and a failed
/// batch is retried instead of permanently killing persistence.
/// </summary>
public sealed class SqliteMetricHistoryPersistenceService : IMetricHistoryPersistenceService, IDisposable
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(750);
    private const int MaxRowsPerBatch = 500;

    private readonly string _connectionString;
    private readonly Channel<(string MetricId, DateTime Timestamp, double Value)> _writeQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _backgroundWriterTask;
    private DateTime _lastRecordUtc = DateTime.MinValue;
    private DateTime _lastCleanup = DateTime.MinValue;
    private bool _disposed;

    public SqliteMetricHistoryPersistenceService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemMonitorTool");
        Directory.CreateDirectory(folder);
        _connectionString = $"Data Source={Path.Combine(folder, "history.db")};Pooling=True";

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            Configure(connection);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS metric_history (
                    metric_id TEXT NOT NULL,
                    timestamp INTEGER NOT NULL,
                    value REAL NOT NULL);
                CREATE INDEX IF NOT EXISTS idx_metric_history_lookup
                    ON metric_history (metric_id, timestamp);
                """;
            cmd.ExecuteNonQuery();
        }

        _writeQueue = Channel.CreateBounded<(string, DateTime, double)>(
            new BoundedChannelOptions(20_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        _backgroundWriterTask = Task.Run(BackgroundWriteLoopAsync);
    }

    private static void Configure(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
    }

    public void Record(IReadOnlyList<MetricReading> snapshot)
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        if (now - _lastRecordUtc < SampleInterval) return;
        _lastRecordUtc = now;

        for (var i = 0; i < snapshot.Count; i++)
        {
            var reading = snapshot[i];
            if (!reading.IsAvailable || reading.Kind == MetricKind.Text) continue;
            if (double.IsNaN(reading.Value) || double.IsInfinity(reading.Value)) continue;

            _writeQueue.Writer.TryWrite((reading.Id, now, reading.Value));
        }
    }

    private async Task BackgroundWriteLoopAsync()
    {
        var reader = _writeQueue.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    using var connection = new SqliteConnection(_connectionString);
                    await connection.OpenAsync(_cts.Token).ConfigureAwait(false);
                    Configure(connection);

                    using var transaction = connection.BeginTransaction();
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText =
                        "INSERT INTO metric_history (metric_id, timestamp, value) VALUES ($id, $ts, $val);";
                    var id = cmd.Parameters.Add("$id", SqliteType.Text);
                    var ts = cmd.Parameters.Add("$ts", SqliteType.Integer);
                    var val = cmd.Parameters.Add("$val", SqliteType.Real);
                    cmd.Prepare();

                    var rows = 0;
                    while (rows < MaxRowsPerBatch && reader.TryRead(out var point))
                    {
                        id.Value = point.MetricId;
                        ts.Value = point.Timestamp.Ticks;
                        val.Value = point.Value;
                        cmd.ExecuteNonQuery();
                        rows++;
                    }

                    transaction.Commit();
                    MaybeCleanup(connection);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[MetricHistory] batch write failed: {ex.Message}");
                    await Task.Delay(250, _cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during forced shutdown.
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[MetricHistory] writer stopped: {ex}");
        }
    }

    private void MaybeCleanup(SqliteConnection connection)
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanup < CleanupInterval) return;
        _lastCleanup = now;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM metric_history WHERE timestamp < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", (now - RetentionWindow).Ticks);
        cmd.ExecuteNonQuery();
    }

    public async Task<IReadOnlyList<MetricHistoryPoint>> GetHistoryAsync(string metricId, DateTime since)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT timestamp, value FROM metric_history
            WHERE metric_id = $id AND timestamp >= $since
            ORDER BY timestamp;
            """;
        cmd.Parameters.AddWithValue("$id", metricId);
        cmd.Parameters.AddWithValue("$since", since.Ticks);

        var results = new List<MetricHistoryPoint>();
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            results.Add(new MetricHistoryPoint(
                new DateTime(reader.GetInt64(0), DateTimeKind.Utc).ToLocalTime(),
                reader.GetDouble(1)));
        }

        return results;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writeQueue.Writer.TryComplete();

        try
        {
            if (!_backgroundWriterTask.Wait(TimeSpan.FromSeconds(2)))
            {
                _cts.Cancel();
                _backgroundWriterTask.Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch
        {
            // Best-effort shutdown — don't let a slow flush block app exit.
        }

        _cts.Dispose();
    }
}

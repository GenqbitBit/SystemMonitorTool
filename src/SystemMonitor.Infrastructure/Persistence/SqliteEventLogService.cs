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
/// Persists app/system events (alerts, threshold crossings, config changes,
/// errors, session starts) to the same local SQLite database used for
/// metric history, backing the read-only in-app Logs viewer.
///
/// Design notes:
///  - Reads use per-operation connections (safe for concurrency).
///  - Writes go through a background Channel; LogEvent() just enqueues and returns immediately.
///  - Events use a 10-day retention window. Cleanup is checked every 15 minutes.
///  - Implements IAsyncDisposable for graceful shutdown: the writer channel is completed
///    first and the background loop is allowed to drain naturally; cancellation is only
///    used as a timeout fallback, not the primary shutdown signal, so pending events are
///    not silently dropped on normal shutdown.
/// </summary>
public sealed class SqliteEventLogService : IEventLogService, IAsyncDisposable, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly Channel<(string Type, DateTime Timestamp, string Message, string? Metadata)> _writeQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _backgroundWriterTask;

    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private DateTime _lastCleanup = DateTime.MinValue;
    private bool _disposed;
    private long _droppedEventCount;

    public SqliteEventLogService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemMonitorTool");

        Directory.CreateDirectory(appDataFolder);

        var dbPath = Path.Combine(appDataFolder, "history.db");
        _connectionString = $"Data Source={dbPath};Pooling=True;";

        // Open once and reuse for the lifetime of the service.
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        InitializeDatabase();

        // Bounded channel to prevent unbounded memory growth if writes outpace SQLite.
        // Uses DropOldest to shed oldest events when the buffer is full, keeping logging
        // non-blocking. Dropped events are counted via DroppedEventCount.
        _writeQueue = Channel.CreateBounded<(string, DateTime, string, string?)>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        _backgroundWriterTask = Task.Run(BackgroundWriteLoopAsync);
    }

    private void InitializeDatabase()
    {
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();

        using var create = _connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp INTEGER NOT NULL,
                type TEXT NOT NULL,
                message TEXT NOT NULL,
                metadata TEXT
            );
            """;
        create.ExecuteNonQuery();

        using var indexLookup = _connection.CreateCommand();
        indexLookup.CommandText = """
            CREATE INDEX IF NOT EXISTS idx_events_lookup
                ON events (type, timestamp);
            """;
        indexLookup.ExecuteNonQuery();

        using var indexTimestamp = _connection.CreateCommand();
        indexTimestamp.CommandText = """
            CREATE INDEX IF NOT EXISTS idx_events_timestamp
                ON events (timestamp);
            """;
        indexTimestamp.ExecuteNonQuery();
    }

    public void LogEvent(string type, string message, string? metadata = null)
    {
        if (_disposed)
            return;

        // Guard against null/empty type to avoid database constraint violations.
        if (string.IsNullOrEmpty(type))
            type = "Unknown";

        if (!_writeQueue.Writer.TryWrite((type, DateTime.UtcNow, message, metadata)))
        {
            Interlocked.Increment(ref _droppedEventCount);
        }
    }

    /// <summary>
    /// Number of events dropped because the write buffer was full.
    /// </summary>
    public long DroppedEventCount => Interlocked.Read(ref _droppedEventCount);

    private async Task BackgroundWriteLoopAsync()
    {
        try
        {
            var reader = _writeQueue.Reader;

            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    using var transaction = _connection.BeginTransaction();

                    using var insert = _connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO events (timestamp, type, message, metadata)
                        VALUES ($ts, $type, $msg, $meta);
                        """;

                    var tsParam = insert.Parameters.Add("$ts", SqliteType.Integer);
                    var typeParam = insert.Parameters.Add("$type", SqliteType.Text);
                    var msgParam = insert.Parameters.Add("$msg", SqliteType.Text);
                    var metaParam = insert.Parameters.Add("$meta", SqliteType.Text);

                    // Explicitly prepare the statement once per batch for better performance.
                    insert.Prepare();

                    while (reader.TryRead(out var entry))
                    {
                        tsParam.Value = entry.Timestamp.Ticks;
                        typeParam.Value = entry.Type;
                        msgParam.Value = entry.Message;
                        metaParam.Value = (object?)entry.Metadata ?? DBNull.Value;

                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    MaybeCleanup();

                    // Reset the counter after the batch has been processed.
                    Interlocked.Exchange(ref _droppedEventCount, 0);
                }
                catch (Exception)
                {
                    // Keep the writer alive for future events.
                    // The transaction is rolled back automatically when disposed.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected only when the shutdown timeout forces cancellation.
        }
        catch (Exception)
        {
            // Background writer failed unexpectedly.
        }
    }

    private void MaybeCleanup()
    {
        var now = DateTime.UtcNow;

        if (now - _lastCleanup < CleanupInterval)
            return;

        _lastCleanup = now;

        var cutoff = now - RetentionWindow;

        using var delete = _connection.CreateCommand();
        delete.CommandText = "DELETE FROM events WHERE timestamp < $cutoff;";
        delete.Parameters.AddWithValue("$cutoff", cutoff.Ticks);

        delete.ExecuteNonQuery();
    }

    public async Task<IReadOnlyList<EventLogEntry>> GetEventsAsync(
        string? type = null,
        DateTime? since = null,
        int limit = 200)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var select = connection.CreateCommand();

        select.CommandText = """
            SELECT timestamp, type, message, metadata FROM events
            WHERE ($type IS NULL OR type = $type)
              AND ($since IS NULL OR timestamp >= $since)
            ORDER BY timestamp DESC
            LIMIT $limit;
            """;

        // Convert 'since' to UTC ticks because the database stores UTC ticks.
        var sinceUtcTicks = since?.ToUniversalTime().Ticks;

        select.Parameters.AddWithValue("$type", (object?)type ?? DBNull.Value);
        select.Parameters.AddWithValue("$since", (object?)sinceUtcTicks ?? DBNull.Value);
        select.Parameters.AddWithValue("$limit", limit);

        var results = new List<EventLogEntry>();

        using var readerResult = await select.ExecuteReaderAsync().ConfigureAwait(false);

        while (await readerResult.ReadAsync().ConfigureAwait(false))
        {
            var timestamp = new DateTime(
                readerResult.GetInt64(0),
                DateTimeKind.Utc).ToLocalTime();

            var type_ = readerResult.GetString(1);
            var message = readerResult.GetString(2);
            var metadata = readerResult.IsDBNull(3)
                ? null
                : readerResult.GetString(3);

            results.Add(new EventLogEntry(
                timestamp,
                type_,
                message,
                metadata));
        }

        return results;
    }

    public async Task DeleteAllEventsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM events;";
        await delete.ExecuteNonQueryAsync().ConfigureAwait(false);

        using var reset = connection.CreateCommand();
        reset.CommandText = "DELETE FROM sqlite_sequence WHERE name = 'events';";
        await reset.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    #region IDisposable & IAsyncDisposable

    /// <summary>
    /// Synchronous dispose. Blocks on the async path so both share one shutdown
    /// implementation instead of drifting out of sync.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gracefully disposes the service. Completes the write channel and lets the
    /// background writer drain naturally so pending events are flushed; only falls
    /// back to cancellation if draining doesn't finish within <see cref="DrainTimeout"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _writeQueue.Writer.TryComplete();

        var drainTask = _backgroundWriterTask;
        var timeoutTask = Task.Delay(DrainTimeout);

        var completed = await Task.WhenAny(
            drainTask,
            timeoutTask).ConfigureAwait(false);

        if (completed != drainTask)
        {
            _cts.Cancel();

            try
            {
                await drainTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected once cancellation is requested.
            }
            catch (Exception)
            {
                // Background writer failed while shutting down.
            }
        }

        _cts.Dispose();
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    #endregion
}
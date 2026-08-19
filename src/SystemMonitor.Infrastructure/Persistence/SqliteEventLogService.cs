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
///  - Same connection is opened once and reused for the lifetime of this
///    service, instead of opening a new SqliteConnection per batch — this
///    is the fix identified for the metric persistence service, applied
///    here from the start.
///  - Writes go through a background Channel; LogEvent() just enqueues and
///    returns immediately.
///  - Events use a 10-day retention window. Cleanup is checked every
///    15 minutes after a successful write batch, keeping the incident log
///    useful without allowing the events table to grow indefinitely.
/// </summary>
public sealed class SqliteEventLogService : IEventLogService, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly Channel<(string Type, DateTime Timestamp, string Message, string? Metadata)> _writeQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _backgroundWriterTask;

    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private DateTime _lastCleanup = DateTime.MinValue;

    public SqliteEventLogService()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemMonitorTool");

        Directory.CreateDirectory(appDataFolder);

        var dbPath = Path.Combine(appDataFolder, "history.db");
        _connectionString = $"Data Source={dbPath}";

        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        InitializeDatabase();

        _writeQueue = Channel.CreateUnbounded<(string, DateTime, string, string?)>();
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
            CREATE INDEX IF NOT EXISTS idx_events_lookup
                ON events (type, timestamp);
            """;

        create.ExecuteNonQuery();
    }

    public void LogEvent(
        string type,
        string message,
        string? metadata = null)
    {
        _writeQueue.Writer.TryWrite(
            (type, DateTime.UtcNow, message, metadata));
    }

    private async Task BackgroundWriteLoopAsync()
    {
        var reader = _writeQueue.Reader;

        while (await reader
            .WaitToReadAsync(_cts.Token)
            .ConfigureAwait(false))
        {
            using var transaction = _connection.BeginTransaction();

            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO events (timestamp, type, message, metadata)
                VALUES ($ts, $type, $msg, $meta);
                """;

            var tsParam = insert.Parameters.Add(
                "$ts",
                SqliteType.Integer);

            var typeParam = insert.Parameters.Add(
                "$type",
                SqliteType.Text);

            var msgParam = insert.Parameters.Add(
                "$msg",
                SqliteType.Text);

            var metaParam = insert.Parameters.Add(
                "$meta",
                SqliteType.Text);

            while (reader.TryRead(out var entry))
            {
                tsParam.Value = entry.Timestamp.Ticks;
                typeParam.Value = entry.Type;
                msgParam.Value = entry.Message;
                metaParam.Value =
                    (object?)entry.Metadata ?? DBNull.Value;

                insert.ExecuteNonQuery();
            }

            transaction.Commit();

            MaybeCleanup();
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
        delete.CommandText =
            "DELETE FROM events WHERE timestamp < $cutoff;";

        delete.Parameters.AddWithValue(
            "$cutoff",
            cutoff.Ticks);

        delete.ExecuteNonQuery();
    }

    public async Task<IReadOnlyList<EventLogEntry>> GetEventsAsync(
        string? type = null,
        DateTime? since = null,
        int limit = 200)
    {
        using var select = _connection.CreateCommand();

        select.CommandText = """
            SELECT timestamp, type, message, metadata FROM events
            WHERE ($type IS NULL OR type = $type)
              AND ($since IS NULL OR timestamp >= $since)
            ORDER BY timestamp DESC
            LIMIT $limit;
            """;

        select.Parameters.AddWithValue(
            "$type",
            (object?)type ?? DBNull.Value);

        select.Parameters.AddWithValue(
            "$since",
            (object?)since?.Ticks ?? DBNull.Value);

        select.Parameters.AddWithValue(
            "$limit",
            limit);

        var results = new List<EventLogEntry>();

        using var readerResult =
            await select.ExecuteReaderAsync()
                .ConfigureAwait(false);

        while (await readerResult
            .ReadAsync()
            .ConfigureAwait(false))
        {
            var timestamp = new DateTime(
                readerResult.GetInt64(0),
                DateTimeKind.Utc).ToLocalTime();

            var type_ = readerResult.GetString(1);
            var message = readerResult.GetString(2);

            var metadata = readerResult.IsDBNull(3)
                ? null
                : readerResult.GetString(3);

            results.Add(
                new EventLogEntry(
                    timestamp,
                    type_,
                    message,
                    metadata));
        }

        return results;
    }

    public void Dispose()
    {
        _writeQueue.Writer.TryComplete();

        _cts.Cancel();

        try
        {
            _backgroundWriterTask.Wait(
                TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort shutdown.
        }

        _cts.Dispose();
        _connection.Dispose();
    }
}
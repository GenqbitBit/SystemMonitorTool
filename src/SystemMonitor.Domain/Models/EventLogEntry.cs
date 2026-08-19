namespace SystemMonitor.Domain.Models;

public sealed record EventLogEntry(
    DateTime Timestamp,
    string Type,
    string Message,
    string? Metadata);
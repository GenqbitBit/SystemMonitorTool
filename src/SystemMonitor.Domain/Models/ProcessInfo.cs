using System;

namespace SystemMonitor.Domain.Models;

/// <summary>
/// Value-equatable so collection diffing can tell "same process, same numbers"
/// from "a new sample object for a process that did not change". Without this,
/// every process refresh replaced every bound row (reference equality only),
/// firing PropertyChanged and re-running row templates for no visual change.
/// </summary>
public class ProcessInfo : IEquatable<ProcessInfo>
{
    public int ProcessId { get; set; }
    public string Name { get; set; } = string.Empty;

    // Share of CPU between two samples; the service will compute it
    // from TotalProcessorTime deltas — driver-free.
    public double CpuPercent { get; set; }

    // Physical memory currently charged to this process (WorkingSet64 / 1024 / 1024).
    public double WorkingSetMB { get; set; }

    public int ThreadCount { get; set; }

    public bool Equals(ProcessInfo? other) =>
        other is not null
        && ProcessId == other.ProcessId
        && CpuPercent.Equals(other.CpuPercent)
        && WorkingSetMB.Equals(other.WorkingSetMB)
        && ThreadCount == other.ThreadCount
        && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ProcessInfo);

    public override int GetHashCode() =>
        HashCode.Combine(ProcessId, CpuPercent, WorkingSetMB, ThreadCount, Name);
}
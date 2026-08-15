using System;
using System.Collections.Generic;

namespace SystemMonitor.Domain.Models;

public class OperatingSystemInfo
{
    // Which OS family we run on — RuntimeInformation, driver-free.
    public OperatingSystemPlatform Platform { get; set; }

    // Friendly name, e.g. "Windows 11", "Ubuntu 24.04", "macOS 15".
    public string OsName { get; set; } = string.Empty;

    // Raw runtime description, for diagnostics.
    public string OsVersion { get; set; } = string.Empty;

    // Time since boot from Environment.TickCount64 — driver-free, always present.
    public TimeSpan Uptime { get; set; }

    // Running processes — Process.GetProcesses(), driver-free.
    public int ProcessCount { get; set; }

    // Sum of threads across processes; best-effort, 0 if the OS refuses.
    public int ThreadCount { get; set; }

    // System-wide open handles. Windows-only; null on other platforms,
    // shown as an honest "—".
    public long? HandleCount { get; set; }

    // Heaviest consumers right now, pre-sorted by the service.
    // Mirrors how CpuInfo carries its Temperatures.
    public List<ProcessInfo> TopProcesses { get; set; } = new();
    public List<ProcessInfo> TopProcessesByCpu { get; init; } = new();
    public List<ProcessInfo> TopProcessesByMemory { get; init; } = new();
}
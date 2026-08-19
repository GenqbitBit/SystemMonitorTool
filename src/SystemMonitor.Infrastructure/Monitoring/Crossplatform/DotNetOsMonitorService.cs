using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.CrossPlatform;

// Platform-neutral OS monitor: everything here comes from the .NET BCL,
// so the same code runs on Windows, Linux and macOS — no kernel driver.
//
// Stateful by design: CpuPercent is a rate, and a rate needs two samples.
// The previous per-process CPU times live in this instance — which is why
// the DI registration (Step 4) must be a singleton.
public class DotNetOsMonitorService : IOsMonitorService
{
    private const int TopProcessCount = 8;

    private readonly object _gate = new();
    private Dictionary<int, TimeSpan> _previousCpuTimes = new();
    private DateTime _previousSampleUtc;
    private OperatingSystemInfo? _lastInfo;

    public OperatingSystemInfo? LastInfo
    {
        get
        {
            lock (_gate)
                return _lastInfo;
        }
    }

    public OperatingSystemInfo GetCurrentInfo()
    {
        lock (_gate) // shared state below; one reader/writer at a time.
        {
            var now = DateTime.UtcNow;
            var wallDelta = _previousSampleUtc == DateTime.MinValue
                ? TimeSpan.Zero
                : now - _previousSampleUtc;

            var processes = Process.GetProcesses();
            try
            {
                var sampled = new List<ProcessInfo>(processes.Length);
                var nextCpuTimes = new Dictionary<int, TimeSpan>(processes.Length);
                var topProcesses = new List<ProcessInfo>(TopProcessCount);
                var topProcessesByCpu = new List<ProcessInfo>(TopProcessCount);
                var topProcessesByMemory = new List<ProcessInfo>(TopProcessCount);
                var threadTotal = 0;
                long? handleTotal = OperatingSystem.IsWindows() ? 0 : null;

                foreach (var process in processes)
                {
                    try
                    {
                        var cpuTime = process.TotalProcessorTime;
                        nextCpuTimes[process.Id] = cpuTime;

                        // Rate = CPU-time delta / wall-time delta, spread over all cores.
                        var previous = _previousCpuTimes.TryGetValue(process.Id, out var prev)
                            ? prev : cpuTime; // new process: first sample is 0%.
                        var cpuPercent = wallDelta == TimeSpan.Zero
                            ? 0
                            : Math.Clamp(Math.Round(
                                100d * (cpuTime - previous).TotalMilliseconds
                                / (wallDelta.TotalMilliseconds * Environment.ProcessorCount), 2),
                                0, 100);

                        var sample = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            Name = process.ProcessName,
                            CpuPercent = cpuPercent,
                            WorkingSetMB = Math.Round(process.WorkingSet64 / 1024d / 1024d, 1)
                        };

                        try { sample.ThreadCount = process.Threads.Count; threadTotal += sample.ThreadCount; }
                        catch { /* some platforms refuse — honest 0 */ }

                        if (handleTotal.HasValue)
                        {
                            try { handleTotal += process.HandleCount; }
                            catch { /* protected process — skip its handles */ }
                        }

                        sampled.Add(sample);
                        AddTopProcess(topProcesses, sample, CompareCombined);
                        AddTopProcess(topProcessesByCpu, sample, CompareCpu);
                        AddTopProcess(topProcessesByMemory, sample, CompareMemory);
                    }
                    catch
                    {
                        // Exited mid-sample or access refused: skipping IS the honest answer.
                    }
                }

                var result = new OperatingSystemInfo
                {
                    Platform = DetectPlatform(),
                    OsName = ReadFriendlyName(),
                    OsVersion = RuntimeInformation.OSDescription,
                    Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                    ProcessCount = processes.Length,
                    ThreadCount = threadTotal,
                    HandleCount = handleTotal,
                    // Combined view: CPU-led ranking, memory as tiebreaker (unchanged).
                    TopProcesses = topProcesses,
                    TopProcessesByCpu = topProcessesByCpu,
                    TopProcessesByMemory = topProcessesByMemory
                };

                // This tick becomes next tick's memory.
                _previousCpuTimes = nextCpuTimes;
                _previousSampleUtc = now;
                _lastInfo = result;
                return result;
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }
    }

    private static void AddTopProcess(
        List<ProcessInfo> top,
        ProcessInfo candidate,
        Comparison<ProcessInfo> comparison)
    {
        var insertIndex = 0;
        while (insertIndex < top.Count && comparison(top[insertIndex], candidate) <= 0)
            insertIndex++;

        if (insertIndex >= TopProcessCount)
            return;

        top.Insert(insertIndex, candidate);
        if (top.Count > TopProcessCount)
            top.RemoveAt(TopProcessCount);
    }

    private static int CompareCombined(ProcessInfo left, ProcessInfo right)
    {
        var cpu = right.CpuPercent.CompareTo(left.CpuPercent);
        return cpu != 0 ? cpu : right.WorkingSetMB.CompareTo(left.WorkingSetMB);
    }

    private static int CompareCpu(ProcessInfo left, ProcessInfo right) =>
        right.CpuPercent.CompareTo(left.CpuPercent);

    private static int CompareMemory(ProcessInfo left, ProcessInfo right) =>
        right.WorkingSetMB.CompareTo(left.WorkingSetMB);

    private static OperatingSystemPlatform DetectPlatform()
    {
        if (OperatingSystem.IsWindows()) return OperatingSystemPlatform.Windows;
        if (OperatingSystem.IsMacOS()) return OperatingSystemPlatform.MacOS;
        if (OperatingSystem.IsLinux()) return OperatingSystemPlatform.Linux;
        return OperatingSystemPlatform.Unknown;
    }

    private static string ReadFriendlyName()
    {
        if (OperatingSystem.IsWindows())
        {
            // Win 10 and 11 both report major 10; the build number tells them apart.
            return Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var line in File.ReadLines("/etc/os-release"))
                {
                    if (line.StartsWith("PRETTY_NAME="))
                        return line.Split('=', 2)[1].Trim('"');
                }
            }
            catch { /* unusual distro without os-release — fall through */ }
            return "Linux";
        }

        if (OperatingSystem.IsMacOS())
            return "macOS"; // raw Darwin version stays in OsVersion; refine later if you like.

        return "Unknown OS";
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
    private static readonly TimeSpan ProcessSampleInterval = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private Dictionary<int, TimeSpan> _previousCpuTimes = new();

    // Second buffer, swapped with _previousCpuTimes after each sample, so the
    // per-PID CPU-time map is not reallocated (and regrown) every two seconds.
    private Dictionary<int, TimeSpan> _nextCpuTimes = new();
    private DateTime _previousSampleUtc;
    private DateTime _lastProcessSampleUtc = DateTime.MinValue;
    private OperatingSystemInfo? _lastInfo;

    // Identity strings never change while the app runs; reading /etc/os-release
    // (or rebuilding the Windows version string) on every process sample was
    // avoidable work on the polling thread.
    private static readonly string StaticOsName = ReadFriendlyName();
    private static readonly string StaticOsVersion = RuntimeInformation.OSDescription;
    private static readonly OperatingSystemPlatform StaticPlatform = DetectPlatform();

    [SupportedOSPlatform("windows")]
    public static bool IsProcessAccessible(Process process)
    {
        if (!OperatingSystem.IsWindows())
            return true;

        var handle = IntPtr.Zero;
        try
        {
            handle = NativeMethods.OpenProcess(0x1000, false, process.Id);
            return handle != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero)
                NativeMethods.CloseHandle(handle);
        }
    }

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
            if (_lastInfo is not null && now - _lastProcessSampleUtc < ProcessSampleInterval)
                return _lastInfo;

            var wallDelta = _previousSampleUtc == DateTime.MinValue
                ? TimeSpan.Zero
                : now - _previousSampleUtc;

            var processes = Process.GetProcesses();
            try
            {
                var previousCpuTimes = _previousCpuTimes;
                var nextCpuTimes = _nextCpuTimes;
                nextCpuTimes.Clear();

                var topProcesses = new List<ProcessInfo>(TopProcessCount);
                var topProcessesByCpu = new List<ProcessInfo>(TopProcessCount);
                var topProcessesByMemory = new List<ProcessInfo>(TopProcessCount);

                // Windows can report system-wide thread/handle totals from one
                // cheap psapi call. The previous code derived "OS threads" by
                // summing Threads.Count over only the first eight processes in
                // enumeration order — expensive per process and simply wrong —
                // and paid a handle-table lookup per process for the same reason.
                var systemCounters = OperatingSystem.IsWindows() ? TryGetWindowsSystemCounts() : null;
                var threadTotal = systemCounters?.Threads ?? 0;
                long? handleTotal = systemCounters?.Handles;
                var collectProcessThreadCounts = !OperatingSystem.IsWindows();

                foreach (var process in processes)
                {
                    try
                    {
                        // No OpenProcess/CloseHandle pre-check here: reading
                        // TotalProcessorTime already performs the access check
                        // (and throws for protected processes). The previous
                        // probe doubled kernel transitions for every process
                        // in the table on every sample.
                        var cpuTime = process.TotalProcessorTime;
                        nextCpuTimes[process.Id] = cpuTime;

                        // Rate = CPU-time delta / wall-time delta, spread over all cores.
                        var previous = previousCpuTimes.TryGetValue(process.Id, out var prev)
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

                        // Non-Windows has no system-wide thread counter in the
                        // BCL, so the totals are still summed per process there.
                        // ThreadCount participates in ProcessInfo value equality.
                        if (collectProcessThreadCounts)
                        {
                            try
                            {
                                sample.ThreadCount = process.Threads.Count;
                                threadTotal += sample.ThreadCount;
                            }
                            catch
                            {
                                sample.ThreadCount = 0;
                            }
                        }
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
                    Platform = StaticPlatform,
                    OsName = StaticOsName,
                    OsVersion = StaticOsVersion,
                    Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                    ProcessCount = processes.Length,
                    ThreadCount = threadTotal,
                    HandleCount = handleTotal,
                    // Combined view: CPU-led ranking, memory as tiebreaker (unchanged).
                    TopProcesses = topProcesses,
                    TopProcessesByCpu = topProcessesByCpu,
                    TopProcessesByMemory = topProcessesByMemory
                };

                // Swap buffers: this tick becomes next tick's memory, and the
                // map read above becomes the scratch map (cleared next sample).
                _previousCpuTimes = nextCpuTimes;
                _nextCpuTimes = previousCpuTimes;
                _previousSampleUtc = now;
                _lastProcessSampleUtc = now;
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

    private readonly struct SystemCounters
    {
        public int? Threads { get; init; }
        public long? Handles { get; init; }
    }

    private static SystemCounters? TryGetWindowsSystemCounts()
    {
        try
        {
            if (!NativeMethods.GetPerformanceInfo(
                    out var info,
                    (uint)Marshal.SizeOf<NativeMethods.PERFORMANCE_INFORMATION>()))
            {
                return null;
            }

            return new SystemCounters
            {
                Threads = info.ThreadCount > 0 ? (int)info.ThreadCount : null,
                Handles = info.HandleCount > 0 ? info.HandleCount : (long?)null
            };
        }
        catch
        {
            return null;
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint processAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        // System-wide process/thread/handle totals, one syscall.
        [StructLayout(LayoutKind.Sequential)]
        internal struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public nuint CommitTotal;
            public nuint CommitLimit;
            public nuint CommitPeak;
            public nuint PhysicalTotal;
            public nuint PhysicalAvailable;
            public nuint SystemCache;
            public nuint KernelTotal;
            public nuint KernelPaged;
            public nuint KernelNonpaged;
            public nuint PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPerformanceInfo(
            out PERFORMANCE_INFORMATION performanceInformation, uint size);
    }
}
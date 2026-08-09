using System;
using System.Diagnostics;
using System.Management;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsCpuMonitorService : ICpuMonitorService
{
    private readonly PerformanceCounter _cpuCounter;

    // OS-provided current frequency in MHz. Unlike the LibreHardwareMonitor
    // clock sensor, this needs no kernel driver — it always works.
    private readonly PerformanceCounter? _frequencyCounter;

    // Static facts, read once — they never change while running.
    private readonly string _modelName = ReadCpuModelName();
    private readonly int _coreCount;
    private readonly int _threadCount;

    public WindowsCpuMonitorService()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue(); // first call always returns 0 — "warms up" the counter

        try
        {
            _frequencyCounter = new PerformanceCounter(
                "Processor Information", "Processor Frequency", "_Total");
            _frequencyCounter.NextValue(); // warm-up, same reason
        }
        catch
        {
            _frequencyCounter = null; // ancient Windows without this counter
        }

        (_coreCount, _threadCount) = ReadCoreCounts();
    }

    public CpuInfo GetCurrentUsage()
    {
        return new CpuInfo
        {
            UsagePercent = _cpuCounter.NextValue(),
            ModelName = _modelName,
            ClockMhz = _frequencyCounter?.NextValue(),
            CoreCount = _coreCount,
            ThreadCount = _threadCount,
            PackagePowerWatts = LibreHardwareMonitorHost.Instance
                .GetPackagePowerWatts(HardwareType.Cpu)
        };
    }

    // The registry needs no privilege and no vendor SDK — identity data
    // should not depend on the flaky SMU/driver channel.
    private static string ReadCpuModelName()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string ?? string.Empty).Trim();
    }

    // WMI, driver-free. NumberOfCores = physical cores;
    // NumberOfLogicalProcessors = threads (cores × SMT).
    private static (int Cores, int Threads) ReadCoreCounts()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            int cores = 0, threads = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                cores += Convert.ToInt32(obj["NumberOfCores"]);
                threads += Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
            }
            if (cores > 0 && threads > 0) return (cores, threads);
        }
        catch
        {
            // fall through to the OS-level fallback
        }
        return (Environment.ProcessorCount, Environment.ProcessorCount);
    }
}
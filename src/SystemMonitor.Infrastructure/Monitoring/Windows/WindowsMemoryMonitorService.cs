using System;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

[SupportedOSPlatform("windows")]
public class WindowsMemoryMonitorService : IMemoryMonitorService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private readonly string _partNumber;
    private readonly string _type;
    private readonly int _speedMhz;
    private readonly string _moduleConfig;
    private readonly string _manufacturer;

    public WindowsMemoryMonitorService()
    {
        // Static identity facts, read once — they never change while running.
        (_partNumber, _type, _speedMhz, _moduleConfig, _manufacturer) = ReadMemoryIdentity();
    }

    private static (string PartNumber, string Type, int SpeedMhz, string Config, string Manufacturer) ReadMemoryIdentity()
    {
        string partNumber = "";
        string type = "Unknown";
        int speedMhz = 0;
        string config = "";
        string manufacturer = "Unknown";

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PartNumber, SMBIOSMemoryType, Speed, Capacity, Manufacturer FROM Win32_PhysicalMemory");
            var sticks = searcher.Get().Cast<ManagementBaseObject>().ToList();

            if (sticks.Count > 0)
            {
                var first = sticks[0];
                type = GetMemoryTypeName(Convert.ToUInt16(first["SMBIOSMemoryType"] ?? 0));
                speedMhz = Convert.ToInt32(first["Speed"] ?? 0);

                manufacturer = first["Manufacturer"]?.ToString()?.Trim() ?? "Unknown";
                if (string.IsNullOrWhiteSpace(manufacturer) || manufacturer.Contains("Undefined"))
                    manufacturer = "Unknown";

                // The part number is the RAM's "name" as far as Windows knows.
                // Distinct + joined, so mixed kits still read sensibly.
                partNumber = string.Join(", ", sticks
                    .Select(s => s["PartNumber"]?.ToString()?.Trim() ?? "")
                    .Where(p => p.Length > 0 && !p.Contains("Unknown"))
                    .Distinct());

                // Group sticks by capacity: "2 x 8 GB", or "1 x 16 GB, 1 x 8 GB" if mixed.
                const double bytesPerGB = 1024L * 1024 * 1024;
                config = string.Join(", ", sticks
                    .GroupBy(s => Convert.ToUInt64(s["Capacity"] ?? 0) / bytesPerGB)
                    .Select(g => $"{g.Count()} x {g.Key} GB"));
            }
        }
        catch
        {
            // WMI failed — keep the "Unknown" defaults.
        }

        return (partNumber, type, speedMhz, config, manufacturer);
    }

    private static string GetMemoryTypeName(ushort smbiosType) => smbiosType switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => $"Unknown ({smbiosType})"
    };

    public MemoryInfo GetCurrentUsage()
    {
        var status = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };
        if (!GlobalMemoryStatusEx(ref status))
            throw new InvalidOperationException("Failed to retrieve memory status from the OS.");

        const double bytesPerMB = 1024 * 1024;
        var totalMB = status.ullTotalPhys / bytesPerMB;
        var availableMB = status.ullAvailPhys / bytesPerMB;
        var usedMB = totalMB - availableMB;

        return new MemoryInfo
        {
            TotalMB = totalMB,
            AvailableMB = availableMB,
            UsedMB = usedMB,
            UsagePercent = status.dwMemoryLoad,
            PartNumber = _partNumber,
            Type = _type,
            SpeedMhz = _speedMhz,
            ModuleConfig = _moduleConfig,
            Manufacturer = _manufacturer,
            Temperatures = GetTemperatures()
        };
    }

    private static List<TemperatureReading> GetTemperatures()
    {
        var readings = new List<TemperatureReading>();
        var host = LibreHardwareMonitorHost.Instance;

        lock (host.UpdateSyncRoot)
        {
            foreach (var hardware in host.Computer.Hardware)
                CollectMemoryTemperatures(hardware, readings);
        }

        return readings;
    }

    private static void CollectMemoryTemperatures(
        IHardware hardware,
        List<TemperatureReading> readings)
    {
        if (hardware.HardwareType == HardwareType.Memory)
        {
            foreach (var sensor in hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature))
            {
                var value = sensor.Value;
                var available = value is { } current && float.IsFinite(current) && current > 0 && current <= 125;

                readings.Add(new TemperatureReading
                {
                    Category = "RAM",
                    HardwareName = hardware.Name,
                    SensorLabel = $"{hardware.Name}: {sensor.Name}",
                    IsAvailable = available,
                    TemperatureCelsius = available ? value!.Value : 0,
                    MinCelsius = available ? sensor.Min ?? value!.Value : 0,
                    MaxCelsius = available ? sensor.Max ?? value!.Value : 0,
                    AverageCelsius = available ? value!.Value : 0,
                    IsPrimary = true
                });
            }
        }

        foreach (var subHardware in hardware.SubHardware)
            CollectMemoryTemperatures(subHardware, readings);
    }
}
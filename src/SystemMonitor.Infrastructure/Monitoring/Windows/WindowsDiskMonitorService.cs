using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

public class WindowsDiskMonitorService : IDiskMonitorService, IDisposable
{
    private readonly string _driveName;
    private readonly PerformanceCounter? _readCounter;
    private readonly PerformanceCounter? _writeCounter;

    private readonly string _model;
    private readonly string _diskType;
    private readonly string _busType;
    private readonly string _fileSystem;

    // Resolved once at construction — the LibreHardwareMonitor handle for this
    // physical disk, used only for its SMART temperature sensor(s).
    private readonly IHardware? _libreHardware;
    private readonly Dictionary<ISensor, (double Sum, int Count)> _temperatureAveraging = new();

    public WindowsDiskMonitorService(string driveName = "C:\\")
    {
        _driveName = driveName;

        try
        {
            _readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            _writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            _readCounter.NextValue();  // warm-up, same reasoning as CpuMonitorService
            _writeCounter.NextValue();
        }
        catch
        {
            _readCounter?.Dispose();
            _writeCounter?.Dispose();
            _readCounter = null;
            _writeCounter = null;
        }

        try
        {
            var drive = new DriveInfo(_driveName);
            _fileSystem = drive.DriveFormat;
        }
        catch
        {
            _fileSystem = "Unknown";
        }

        (_model, _diskType, _busType) = ReadDiskIdentity(_driveName);
        _libreHardware = ResolveDiskHardwareFromLibre(_model);
    }

    // Traces C: -> partition -> physical disk, then asks the modern Windows
    // Storage namespace for model / SSD-vs-HDD / bus type.
    private static (string Model, string DiskType, string BusType) ReadDiskIdentity(string driveLetter)
    {
        string model = "Unknown";
        string diskType = "Unknown";
        string busType = "Unknown";

        try
        {
            string deviceId = driveLetter.TrimEnd('\\', '/').ToUpperInvariant();
            if (!deviceId.EndsWith(":")) deviceId += ":";

            // 1. Logical disk (C:) -> partition
            string? partDeviceId = null;
            using (var logToPartSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDiskToPartition"))
            {
                foreach (ManagementObject assoc in logToPartSearcher.Get())
                {
                    var dep = assoc["Dependent"]?.ToString() ?? "";
                    if (dep.Contains($"\"{deviceId}\""))
                    {
                        partDeviceId = ExtractQuoted(assoc["Antecedent"]?.ToString());
                        break;
                    }
                }
            }
            if (partDeviceId == null) return (model, diskType, busType);

            // 2. Partition -> physical disk drive
            string? diskDriveDeviceId = null;
            using (var partToDriveSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDriveToDiskPartition"))
            {
                foreach (ManagementObject assoc in partToDriveSearcher.Get())
                {
                    var dep = assoc["Dependent"]?.ToString() ?? "";
                    if (dep.Contains($"\"{partDeviceId}\""))
                    {
                        diskDriveDeviceId = ExtractQuoted(assoc["Antecedent"]?.ToString());
                        break;
                    }
                }
            }
            if (diskDriveDeviceId == null) return (model, diskType, busType);

            // "\\.\PHYSICALDRIVE0" -> "0"
            string indexStr = diskDriveDeviceId.Split('\\').LastOrDefault()?.Replace("PHYSICALDRIVE", "") ?? "0";

            // 3. MSFT_PhysicalDisk — DeviceId is a STRING property, so quote it.
            using var physDiskSearcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                $"SELECT Model, MediaType, BusType FROM MSFT_PhysicalDisk WHERE DeviceId = '{indexStr}'");

            var physDisk = physDiskSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            if (physDisk != null)
            {
                model = physDisk["Model"]?.ToString()?.Trim() ?? "Unknown";

                diskType = Convert.ToInt32(physDisk["MediaType"] ?? 0) switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM",
                    _ => "Unspecified"
                };

                busType = Convert.ToInt32(physDisk["BusType"] ?? 0) switch
                {
                    7 => "USB",
                    11 => "SATA",
                    17 => "NVMe",
                    _ => "Other"
                };
            }
            else
            {
                // Fallback: older Win32_DiskDrive at least gives us the model.
                using var driveSearcher = new ManagementObjectSearcher(
                    $"SELECT Model FROM Win32_DiskDrive WHERE DeviceID = '{diskDriveDeviceId.Replace("\\", "\\\\")}'");
                var drive = driveSearcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
                if (drive != null)
                    model = drive["Model"]?.ToString()?.Trim() ?? "Unknown";
            }
        }
        catch
        {
            // WMI failed — keep the "Unknown" defaults.
        }

        return (model, diskType, busType);
    }

    // Matches this drive's WMI model string against LibreHardwareMonitor's
    // Storage hardware list — same name-matching pattern WindowsGpuMonitorService
    // uses for its own device resolution.
    private static IHardware? ResolveDiskHardwareFromLibre(string diskModel)
    {
        if (string.IsNullOrWhiteSpace(diskModel) || diskModel == "Unknown")
        {
            return null;
        }

        var computer = LibreHardwareMonitorHost.Instance.Computer;
        var candidates = computer.Hardware
            .Where(h => h.HardwareType == HardwareType.Storage)
            .ToList();

        var match = candidates.FirstOrDefault(h =>
            string.Equals(h.Name?.Trim(), diskModel.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            Debug.WriteLine(
                $"[Disk] No LibreHardwareMonitor storage device matched model '{diskModel}'. " +
                $"Devices seen: {string.Join(", ", candidates.Select(h => $"'{h.Name}'"))}");
        }

        return match;
    }

    // Pulls the text between the quotes of a WMI reference string,
    // e.g. 'Win32_DiskPartition.DeviceID="Disk #0, Partition #1"' -> 'Disk #0, Partition #1'
    private static string? ExtractQuoted(string? wmiReference)
    {
        if (wmiReference == null) return null;
        int start = wmiReference.IndexOf('"') + 1;
        int end = wmiReference.LastIndexOf('"');
        return start > 0 && end > start ? wmiReference.Substring(start, end - start) : null;
    }

    public DiskInfo GetCurrentUsage()
    {
        var drive = new DriveInfo(_driveName);
        const double bytesPerGB = 1024L * 1024 * 1024;
        const double bytesPerMB = 1024 * 1024;

        var totalGB = drive.TotalSize / bytesPerGB;
        var freeGB = drive.TotalFreeSpace / bytesPerGB;
        var usedGB = totalGB - freeGB;
        var usagePercent = totalGB > 0 ? usedGB / totalGB * 100 : 0;

        return new DiskInfo
        {
            DriveName = drive.Name,
            TotalGB = totalGB,
            FreeGB = freeGB,
            UsedGB = usedGB,
            UsagePercent = usagePercent,
            ReadMBPerSec = (_readCounter?.NextValue() ?? 0) / bytesPerMB,
            WriteMBPerSec = (_writeCounter?.NextValue() ?? 0) / bytesPerMB,
            Model = _model,
            DiskType = _diskType,
            BusType = _busType,
            FileSystem = _fileSystem,
            Temperatures = GetTemperatures()
        };
    }

    // Moved from the old WindowsTemperatureMonitorService, scoped to this drive's
    // own LibreHardware handle. Usually just one "Temperature" SMART sensor.
    private List<TemperatureReading> GetTemperatures()
    {
        if (_libreHardware is null)
        {
            return new List<TemperatureReading>();
        }

        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            var temperatureSensors = _libreHardware.Sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .Where(s => !s.Name.Contains("Warning", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase));

            var readings = temperatureSensors
                .Select(sensor => BuildTemperatureReading(sensor, isPrimary: true))
                .ToList();

            DisambiguateDuplicateLabels(readings);
            return readings;
        }
    }

    private TemperatureReading BuildTemperatureReading(ISensor sensor, bool isPrimary)
    {
        var isRealReading = sensor.Value.HasValue && sensor.Value.Value != 0;
        double average = 0, min = 0, max = 0;

        if (isRealReading)
        {
            var currentValue = sensor.Value!.Value;
            if (_temperatureAveraging.TryGetValue(sensor, out var existing))
            {
                var newSum = existing.Sum + currentValue;
                var newCount = existing.Count + 1;
                _temperatureAveraging[sensor] = (newSum, newCount);
                average = newSum / newCount;
            }
            else
            {
                _temperatureAveraging[sensor] = (currentValue, 1);
                average = currentValue;
            }
            min = sensor.Min ?? currentValue;
            max = sensor.Max ?? currentValue;
        }

        return new TemperatureReading
        {
            SensorLabel = sensor.Name ?? "Unknown",
            IsAvailable = isRealReading,
            TemperatureCelsius = isRealReading ? sensor.Value!.Value : 0,
            MinCelsius = min,
            MaxCelsius = max,
            AverageCelsius = average,
            IsPrimary = isPrimary
        };
    }

    private static void DisambiguateDuplicateLabels(List<TemperatureReading> readings)
    {
        var groups = readings.GroupBy(r => r.SensorLabel);
        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var index = 1;
            foreach (var reading in group)
            {
                reading.SensorLabel = $"{reading.SensorLabel} #{index}";
                index++;
            }
        }
    }

    public void Dispose()
    {
        _readCounter?.Dispose();
        _writeCounter?.Dispose();
    }
}
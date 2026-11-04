using System;
using System.Linq;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

/// <summary>
/// Windows keeper of the motherboard promise.
/// Identity comes from SMBIOS; temperature comes from whichever
/// sensor the board exposes — some expose none, and that is an
/// honest null, not an error.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsMotherboardMonitorService : IMotherboardMonitorService
{
    private readonly Computer _computer = LibreHardwareMonitorHost.Instance.Computer;

    public MotherboardInfo? GetCurrentInfo()
    {
        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            var board = _computer.Hardware
                .FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);

            if (board is null)
                return null; // detection failed; the UI will hide the card

            var temperatures = new System.Collections.Generic.List<TemperatureReading>();
            foreach (var hardware in _computer.Hardware)
                CollectTemperatures(hardware, temperatures);

            double? temperature = temperatures
                .FirstOrDefault(reading => reading.IsAvailable)?.TemperatureCelsius;

            return new MotherboardInfo(
                Model: board.Name,
                Chipset: ChipsetGuess.FromModelName(board.Name) ?? "Unknown",
                TemperatureCelsius: temperature)
            {
                Temperatures = temperatures
            };
        }
    }

    private static void CollectTemperatures(
        IHardware hardware,
        System.Collections.Generic.List<TemperatureReading> readings)
    {
        if (hardware.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO)
        {
            foreach (var sensor in hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature))
            {
                var value = sensor.Value;
                var available = value is { } current && float.IsFinite(current) && current > 0 && current <= 125;

                readings.Add(new TemperatureReading
                {
                    Category = "MOTHERBOARD",
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
            CollectTemperatures(subHardware, readings);
    }

    /// <summary>
    /// The OS exposes no "chipset" fact, so we recognise known chipset
    /// tokens inside the board model name. A heuristic label, not a measurement.
    /// </summary>
    private static class ChipsetGuess
    {
        private static readonly string[] Amd =
        {
            "X870", "B850", "X670", "B650", "A620",
            "X570", "B550", "A520", "X470", "B450",
            "X370", "B350", "A320", "TRX40", "WRX80"
        };

        private static readonly string[] Intel =
        {
            "Z890", "B860", "Z790", "B760", "H770",
            "Z690", "B660", "H610", "Z590", "B560",
            "H510", "Z490", "B460", "H410", "Z390",
            "B360", "H310", "X299"
        };

        public static string? FromModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return null;

            var amd = Amd.FirstOrDefault(c =>
                modelName.Contains(c, StringComparison.OrdinalIgnoreCase));
            if (amd is not null) return $"AMD {amd}";

            var intel = Intel.FirstOrDefault(c =>
                modelName.Contains(c, StringComparison.OrdinalIgnoreCase));
            if (intel is not null) return $"Intel {intel}";

            return null;
        }
    }
}
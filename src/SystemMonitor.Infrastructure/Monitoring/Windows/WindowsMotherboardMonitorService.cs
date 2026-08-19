using System;
using System.Linq;
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
public sealed class WindowsMotherboardMonitorService : IMotherboardMonitorService
{
    private readonly Computer _computer = new() { IsMotherboardEnabled = true };

    public WindowsMotherboardMonitorService() => _computer.Open();

    public MotherboardInfo? GetCurrentInfo()
    {
        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            var board = _computer.Hardware
                .FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);

            if (board is null)
                return null; // detection failed; the UI will hide the card

            // Refresh this hardware's sensors before reading them.
            board.Update();

            // Policy: first temperature sensor found on the board.
            double? temperature = board.Sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .Select(s => (double?)s.Value)
                .FirstOrDefault();

            return new MotherboardInfo(
                Model: board.Name,
                Chipset: ChipsetGuess.FromModelName(board.Name) ?? "Unknown",
                TemperatureCelsius: temperature);
        }
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
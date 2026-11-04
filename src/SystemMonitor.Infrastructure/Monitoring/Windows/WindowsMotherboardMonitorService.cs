using System;
using System.Collections.Generic;
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
    private readonly object _gate = new();
    private IHardware? _board;
    private bool _boardResolved;

    public MotherboardInfo? GetCurrentInfo()
    {
        var host = LibreHardwareMonitorHost.Instance;

        if (!_boardResolved)
        {
            lock (_gate)
            {
                if (!_boardResolved)
                {
                    lock (host.UpdateSyncRoot)
                    {
                        foreach (var hardware in host.Computer.Hardware)
                        {
                            if (hardware.HardwareType == HardwareType.Motherboard)
                            {
                                _board = hardware;
                                break;
                            }
                        }
                    }
                    _boardResolved = true;
                }
            }
        }

        var board = _board;
        if (board is null)
            return null; // detection failed; the UI will hide the card

        var cached = host.GetTemperatureReadings("MOTHERBOARD");
        var temperatures = cached.Count == 0
            ? new List<TemperatureReading>()
            : new List<TemperatureReading>(cached);

        double? temperature = null;
        foreach (var reading in temperatures)
        {
            if (!reading.IsAvailable) continue;
            temperature = reading.TemperatureCelsius;
            break;
        }

        return new MotherboardInfo(
            Model: board.Name,
            Chipset: ChipsetGuess.FromModelName(board.Name) ?? "Unknown",
            TemperatureCelsius: temperature)
        {
            Temperatures = temperatures
        };
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

            foreach (var chipset in Amd)
            {
                if (modelName.Contains(chipset, StringComparison.OrdinalIgnoreCase))
                    return $"AMD {chipset}";
            }

            foreach (var chipset in Intel)
            {
                if (modelName.Contains(chipset, StringComparison.OrdinalIgnoreCase))
                    return $"Intel {chipset}";
            }

            return null;
        }
    }
}

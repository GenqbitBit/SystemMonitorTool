using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public class MacOsMotherboardMonitorService : IMotherboardMonitorService
{
    public MotherboardInfo? GetCurrentInfo()
    {
        using var document = MacOsCommandRunner.ParseJson(
            MacOsCommandRunner.Run("/usr/sbin/system_profiler", "SPHardwareDataType", "-json"));
        if (document is null) return null;

        var hardware = MacOsCommandRunner.Descendants(document.RootElement)
            .FirstOrDefault(element => element.TryGetProperty("machine_name", out _)
                || element.TryGetProperty("chip_type", out _));
        if (hardware.ValueKind == System.Text.Json.JsonValueKind.Undefined) return null;

        var model = MacOsCommandRunner.JsonString(hardware, "machine_name", "model_name") ?? "Unknown";
        var chipset = MacOsCommandRunner.JsonString(hardware, "chip_type", "cpu_type") ?? "Unknown";
        return new MotherboardInfo(model, chipset, null);
    }
}

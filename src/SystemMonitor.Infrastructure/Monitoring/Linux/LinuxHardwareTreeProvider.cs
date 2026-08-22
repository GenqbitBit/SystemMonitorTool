using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux hardware tree provider for thermal and hwmon sensors.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxHardwareTreeProvider : IHardwareTreeProvider
{
    public IReadOnlyList<HardwareTreeNode> DiscoverTree()
    {
        var roots = new List<HardwareTreeNode>();
        var thermal = new HardwareTreeNode { Name = "Thermal zones", Kind = HardwareTreeNodeKind.Hardware };
        foreach (var path in LinuxFileReader.GetDirectories("/sys/class/thermal")
            .Where(path => Path.GetFileName(path).StartsWith("thermal_zone", StringComparison.OrdinalIgnoreCase)))
        {
            var name = LinuxFileReader.ReadText(Path.Combine(path, "type")) ?? Path.GetFileName(path);
            thermal.Children.Add(CreateSensor(name, Path.Combine(path, "temp")));
        }
        if (thermal.Children.Count > 0) roots.Add(thermal);

        var hwmon = new HardwareTreeNode { Name = "Hardware sensors", Kind = HardwareTreeNodeKind.Hardware };
        foreach (var path in LinuxFileReader.GetDirectories("/sys/class/hwmon")
            .Where(path => Path.GetFileName(path).StartsWith("hwmon", StringComparison.OrdinalIgnoreCase)))
        {
            var group = new HardwareTreeNode
            {
                Name = LinuxFileReader.ReadText(Path.Combine(path, "name")) ?? Path.GetFileName(path),
                Kind = HardwareTreeNodeKind.SensorGroup
            };
            foreach (var input in LinuxFileReader.GetFiles(path, "*_input"))
                group.Children.Add(CreateSensor(Path.GetFileNameWithoutExtension(input), input));
            if (group.Children.Count > 0) hwmon.Children.Add(group);
        }
        if (hwmon.Children.Count > 0) roots.Add(hwmon);
        RefreshValues(roots);
        return roots;
    }

    public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
    {
        foreach (var node in Flatten(roots).Where(node => node.Kind == HardwareTreeNodeKind.Sensor))
        {
            var path = node.SensorKey;
            if (path is null) continue;
            if (!LinuxFileReader.TryReadDouble(path, out var value))
            {
                node.IsAvailable = false;
                node.DisplayValue = "N/A";
                continue;
            }
            var display = path.EndsWith("/temp", StringComparison.Ordinal) || path.EndsWith("_input", StringComparison.Ordinal)
                ? value / 1000d : value;
            node.IsAvailable = true;
            node.DisplayValue = $"{display:0.##}";
        }
    }

    private static HardwareTreeNode CreateSensor(string name, string path) => new()
    {
        Name = name,
        Kind = HardwareTreeNodeKind.Sensor,
        SensorKey = path,
        DisplayValue = "N/A"
    };

    private static IEnumerable<HardwareTreeNode> Flatten(IEnumerable<HardwareTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children)) yield return child;
        }
    }
}

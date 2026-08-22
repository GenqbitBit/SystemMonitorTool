using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareTreeProvider : IHardwareTreeProvider
{
    private readonly Computer _computer;

    // NOTE: assumes you already have a shared, opened LibreHardwareMonitor
    // Computer instance (likely inside your existing LibreHardwareMonitorHost).
    // Inject that same instance here rather than creating a second one —
    // opening two Computer instances against the same sensors can cause
    // access conflicts.
    public WindowsHardwareTreeProvider(Computer computer)
    {
        _computer = computer;
    }

    public IReadOnlyList<HardwareTreeNode> DiscoverTree()
    {
        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            var roots = new List<HardwareTreeNode>();
            foreach (var hardware in _computer.Hardware)
            {
                roots.Add(BuildHardwareNode(hardware));
            }
            return roots;
        }
    }

    public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
    {
        lock (LibreHardwareMonitorHost.Instance.UpdateSyncRoot)
        {
            foreach (var root in roots)
            {
                ApplyValues(root, BuildSensorLookup());
            }
        }
    }

    private Dictionary<string, ISensor> BuildSensorLookup()
    {
        var map = new Dictionary<string, ISensor>();
        void Walk(IHardware hw)
        {
            foreach (var sensor in hw.Sensors)
                map[SensorKey(hw, sensor)] = sensor;
            foreach (var sub in hw.SubHardware)
                Walk(sub);
        }
        foreach (var hardware in _computer.Hardware)
            Walk(hardware);
        return map;
    }

    private static void ApplyValues(HardwareTreeNode node, Dictionary<string, ISensor> lookup)
    {
        if (node.Kind == HardwareTreeNodeKind.Sensor && node.SensorKey is not null)
        {
            if (lookup.TryGetValue(node.SensorKey, out var sensor))
            {
                node.IsAvailable = sensor.Value.HasValue;
                node.DisplayValue = FormatSensorValue(sensor);
            }
            else
            {
                node.IsAvailable = false;
                node.DisplayValue = "N/A";
            }
        }

        foreach (var child in node.Children)
            ApplyValues(child, lookup);
    }

    private static HardwareTreeNode BuildHardwareNode(IHardware hardware)
    {
        hardware.Update();

        var node = new HardwareTreeNode
        {
            Name = hardware.Name,
            Kind = HardwareTreeNodeKind.Hardware
        };

        foreach (var group in hardware.Sensors.GroupBy(s => s.SensorType).OrderBy(g => g.Key))
        {
            var groupNode = new HardwareTreeNode
            {
                Name = FormatGroupName(group.Key),
                Kind = HardwareTreeNodeKind.SensorGroup
            };

            foreach (var sensor in group)
            {
                groupNode.Children.Add(new HardwareTreeNode
                {
                    Name = sensor.Name,
                    Kind = HardwareTreeNodeKind.Sensor,
                    SensorKey = SensorKey(hardware, sensor),
                    IsAvailable = sensor.Value.HasValue,
                    DisplayValue = FormatSensorValue(sensor)
                });
            }

            node.Children.Add(groupNode);
        }

        foreach (var sub in hardware.SubHardware)
            node.Children.Add(BuildHardwareNode(sub));

        return node;
    }

    private static string SensorKey(IHardware hardware, ISensor sensor) =>
        $"{hardware.Identifier}/{sensor.Identifier}";

    private static string FormatGroupName(SensorType type) => type switch
    {
        SensorType.Voltage => "Voltages",
        SensorType.Temperature => "Temperatures",
        SensorType.Fan => "Fans",
        SensorType.Clock => "Clocks",
        SensorType.Load => "Load",
        SensorType.Power => "Power",
        _ => type.ToString()
    };

    private static string FormatSensorValue(ISensor sensor)
    {
        if (!sensor.Value.HasValue) return "N/A";
        var unit = sensor.SensorType switch
        {
            SensorType.Voltage => "V",
            SensorType.Temperature => "°C",
            SensorType.Fan => "RPM",
            SensorType.Clock => "MHz",
            SensorType.Load => "%",
            SensorType.Power => "W",
            _ => ""
        };
        return $"{sensor.Value.Value:0.#}{unit}";
    }
}
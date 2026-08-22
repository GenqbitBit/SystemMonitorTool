using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public sealed class MacOsHardwareTreeProvider : IHardwareTreeProvider
{
    public IReadOnlyList<HardwareTreeNode> DiscoverTree()
    {
        var roots = new List<HardwareTreeNode>();
        using var document = MacOsCommandRunner.ParseJson(
            MacOsCommandRunner.Run("/usr/sbin/system_profiler", "SPHardwareDataType", "SPDisplaysDataType", "SPStorageDataType", "-json"));
        if (document is null) return roots;

        var inventory = new HardwareTreeNode { Name = "macOS hardware", Kind = HardwareTreeNodeKind.Hardware };
        foreach (var element in MacOsCommandRunner.Descendants(document.RootElement))
        {
            var name = MacOsCommandRunner.JsonString(element, "machine_name", "sppci_model", "_name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            inventory.Children.Add(new HardwareTreeNode
            {
                Name = name,
                Kind = HardwareTreeNodeKind.SensorGroup
            });
        }
        if (inventory.Children.Count > 0) roots.Add(inventory);
        return roots;
    }

    public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
    {
    }
}

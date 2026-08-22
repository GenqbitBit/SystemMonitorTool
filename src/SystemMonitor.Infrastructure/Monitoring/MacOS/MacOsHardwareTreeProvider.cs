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
        return new List<HardwareTreeNode>();
    }

    public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
    {
    }
}

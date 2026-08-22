using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux hardware tree provider (placeholder).
/// Full implementation will discover hardware from /sys/ and other Linux APIs.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxHardwareTreeProvider : IHardwareTreeProvider
{
    public IReadOnlyList<HardwareTreeNode> DiscoverTree()
    {
        // Placeholder implementation - return empty tree
        return new List<HardwareTreeNode>();
    }

    public void RefreshValues(IReadOnlyList<HardwareTreeNode> roots)
    {
        // Placeholder implementation - no-op
    }
}

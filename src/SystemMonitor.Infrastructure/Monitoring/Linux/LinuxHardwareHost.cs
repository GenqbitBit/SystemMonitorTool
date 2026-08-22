using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

/// <summary>
/// Linux hardware refresh service (placeholder).
/// Coordinates hardware sensor updates across all Linux monitoring services.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxHardwareHost : IHardwareRefreshService, IDisposable
{
    /// <summary>Gets the singleton instance.</summary>
    public static LinuxHardwareHost Instance { get; } = new();

    /// <summary>Synchronization root for hardware updates.</summary>
    public object UpdateSyncRoot { get; } = new();

    public void RefreshAll()
    {
        // Placeholder implementation - no-op
        lock (UpdateSyncRoot)
        {
            // To be implemented
        }
    }

    public void Dispose()
    {
        // Placeholder implementation - no-op
    }
}

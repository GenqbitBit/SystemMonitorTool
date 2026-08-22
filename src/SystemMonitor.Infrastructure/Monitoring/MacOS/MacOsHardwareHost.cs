using System;
using System.Runtime.Versioning;
using SystemMonitor.Application.Interfaces;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

[SupportedOSPlatform("macos")]
public sealed class MacOsHardwareHost : IHardwareRefreshService, IDisposable
{
    public static MacOsHardwareHost Instance { get; } = new();

    public object UpdateSyncRoot { get; } = new();

    public void RefreshAll()
    {
        lock (UpdateSyncRoot)
        {
            // To be implemented
        }
    }

    public void Dispose()
    {
    }
}

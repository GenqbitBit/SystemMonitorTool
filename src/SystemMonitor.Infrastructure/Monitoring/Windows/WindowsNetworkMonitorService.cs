using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring.Windows;

/// <summary>
/// Network monitoring using cross-platform BCL APIs.
/// Note: Despite being in the Windows folder, this service works on all platforms
/// because System.Net.NetworkInformation is cross-platform.
/// </summary>
public class WindowsNetworkMonitorService : INetworkMonitorService
{
    // Enumerating network interfaces is comparatively expensive (COM/registry
    // backed on Windows) and the set changes rarely, so the active list is
    // cached and only rebuilt periodically. The byte counters themselves are
    // still read on every sample.
    private static readonly TimeSpan InterfaceRefreshInterval = TimeSpan.FromSeconds(10);

    private NetworkInterface[] _activeInterfaces = Array.Empty<NetworkInterface>();
    private DateTime _interfacesRefreshedUtc = DateTime.MinValue;

    private long _lastBytesReceived;
    private long _lastBytesSent;
    private long _lastSampleTimestamp;

    public WindowsNetworkMonitorService()
    {
        (_lastBytesReceived, _lastBytesSent) = GetTotalBytes(DateTime.UtcNow);
        _lastSampleTimestamp = Stopwatch.GetTimestamp();
    }

    public NetworkInfo GetCurrentUsage()
    {
        // Stopwatch-based timing: the previous wall-clock delta could go
        // negative (and clamp the rate to 0 for a tick) when the system clock
        // was adjusted or resynchronised.
        var sampleTimestamp = Stopwatch.GetTimestamp();
        var (bytesReceived, bytesSent) = GetTotalBytes(DateTime.UtcNow);

        var elapsedSeconds = Stopwatch.GetElapsedTime(_lastSampleTimestamp, sampleTimestamp).TotalSeconds;
        if (elapsedSeconds <= 0) elapsedSeconds = 1; // guard against div-by-zero on rapid calls

        const double bytesPerKB = 1024;

        var downloadKBPerSec = (bytesReceived - _lastBytesReceived) / bytesPerKB / elapsedSeconds;
        var uploadKBPerSec = (bytesSent - _lastBytesSent) / bytesPerKB / elapsedSeconds;

        _lastBytesReceived = bytesReceived;
        _lastBytesSent = bytesSent;
        _lastSampleTimestamp = sampleTimestamp;

        return new NetworkInfo
        {
            DownloadKBPerSec = Math.Max(0, downloadKBPerSec),
            UploadKBPerSec = Math.Max(0, uploadKBPerSec)
        };
    }

    private (long bytesReceived, long bytesSent) GetTotalBytes(DateTime nowUtc)
    {
        var activeInterfaces = GetActiveInterfaces(nowUtc);

        long received = 0;
        long sent = 0;

        for (var i = 0; i < activeInterfaces.Length; i++)
        {
            try
            {
                var stats = activeInterfaces[i].GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch (NetworkInformationException) { }
            catch (InvalidOperationException) { }
        }

        return (received, sent);
    }

    private NetworkInterface[] GetActiveInterfaces(DateTime nowUtc)
    {
        var cached = _activeInterfaces;
        if (_interfacesRefreshedUtc != DateTime.MinValue
            && nowUtc - _interfacesRefreshedUtc < InterfaceRefreshInterval)
        {
            return cached;
        }

        NetworkInterface[] allInterfaces;
        try { allInterfaces = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { return cached; }
        catch (PlatformNotSupportedException) { return cached; }

        var active = new List<NetworkInterface>(allInterfaces.Length);
        for (var i = 0; i < allInterfaces.Length; i++)
        {
            var nic = allInterfaces[i];
            if (nic.OperationalStatus == OperationalStatus.Up
                && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                active.Add(nic);
            }
        }

        _activeInterfaces = active.ToArray();
        _interfacesRefreshedUtc = nowUtc;
        return _activeInterfaces;
    }
}
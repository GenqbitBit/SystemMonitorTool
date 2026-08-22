using System;
using System.Linq;
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
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastSampleTime;

    public WindowsNetworkMonitorService()
    {
        (_lastBytesReceived, _lastBytesSent) = GetTotalBytes();
        _lastSampleTime = DateTime.UtcNow;
    }

    public NetworkInfo GetCurrentUsage()
    {
        var (bytesReceived, bytesSent) = GetTotalBytes();
        var now = DateTime.UtcNow;

        var elapsedSeconds = (now - _lastSampleTime).TotalSeconds;
        if (elapsedSeconds <= 0) elapsedSeconds = 1; // guard against div-by-zero on rapid calls

        const double bytesPerKB = 1024;

        var downloadKBPerSec = (bytesReceived - _lastBytesReceived) / bytesPerKB / elapsedSeconds;
        var uploadKBPerSec = (bytesSent - _lastBytesSent) / bytesPerKB / elapsedSeconds;

        _lastBytesReceived = bytesReceived;
        _lastBytesSent = bytesSent;
        _lastSampleTime = now;

        return new NetworkInfo
        {
            DownloadKBPerSec = Math.Max(0, downloadKBPerSec),
            UploadKBPerSec = Math.Max(0, uploadKBPerSec)
        };
    }

    private static (long bytesReceived, long bytesSent) GetTotalBytes()
    {
        NetworkInterface[] allInterfaces;
        try { allInterfaces = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { return (0, 0); }
        catch (PlatformNotSupportedException) { return (0, 0); }

        var activeInterfaces = allInterfaces
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                       && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        long received = 0;
        long sent = 0;

        foreach (var nic in activeInterfaces)
        {
            try
            {
                var stats = nic.GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch (NetworkInformationException) { }
            catch (InvalidOperationException) { }
        }

        return (received, sent);
    }
}
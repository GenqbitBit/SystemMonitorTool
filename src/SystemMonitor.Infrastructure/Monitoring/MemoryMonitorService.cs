using System.Runtime.InteropServices;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Monitoring;

public class MemoryMonitorService : IMemoryMonitorService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public MemoryInfo GetCurrentUsage()
    {
        var status = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };

        if (!GlobalMemoryStatusEx(ref status))
            throw new InvalidOperationException("Failed to retrieve memory status from the OS.");

        const double bytesPerMB = 1024 * 1024;

        var totalMB = status.ullTotalPhys / bytesPerMB;
        var availableMB = status.ullAvailPhys / bytesPerMB;
        var usedMB = totalMB - availableMB;

        return new MemoryInfo
        {
            TotalMB = totalMB,
            AvailableMB = availableMB,
            UsedMB = usedMB,
            UsagePercent = status.dwMemoryLoad // Windows already computes this 
        };
    }
}
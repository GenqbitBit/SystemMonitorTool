using System.Collections.Generic;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IGpuMonitorService
{
    IReadOnlyList<GpuInfo> GetCurrentUsage();

    // Cheap, no-live-read device list — lets other services (e.g. temperature)
    // match GPU hardware to the SAME Index/Name/IsIntegrated this service uses,
    // so "GPU 0" means the same physical device everywhere in the app.
    IReadOnlyList<GpuDeviceIdentity> GetDeviceIdentities();
}
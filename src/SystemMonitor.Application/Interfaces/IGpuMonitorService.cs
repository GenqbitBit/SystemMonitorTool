using System.Collections.Generic;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IGpuMonitorService
{
    IReadOnlyList<GpuInfo> GetCurrentUsage();
}
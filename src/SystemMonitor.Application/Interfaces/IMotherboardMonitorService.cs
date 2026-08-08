using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IMotherboardMonitorService
{
    MotherboardInfo? GetCurrentInfo();
}
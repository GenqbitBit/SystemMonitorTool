using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface IHardwareTreeProvider
{
    /// Full rescan: walks hardware/sensors and rebuilds the tree shape + values.
    /// Slow-ish, only called at startup or on manual Refresh.
    IReadOnlyList<HardwareTreeNode> DiscoverTree();

    /// Cheap: re-reads current sensor values and updates DisplayValue/IsAvailable
    /// on the existing node instances in place (no shape rebuild).
    void RefreshValues(IReadOnlyList<HardwareTreeNode> roots);
}
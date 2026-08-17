namespace SystemMonitor.Domain.Models;

public enum HardwareTreeNodeKind
{
    Hardware,
    SensorGroup,
    Sensor
}

public sealed class HardwareTreeNode
{
    public required string Name { get; init; }
    public required HardwareTreeNodeKind Kind { get; init; }

    // Leaf-only (Kind == Sensor)
    public string? DisplayValue { get; set; } = "N/A";
    public bool IsAvailable { get; set; }

    // Stable key used to match this node back to its live sensor on refresh
    // (e.g. "motherboard.fan.upper-front"). Only meaningful for Kind == Sensor.
    public string? SensorKey { get; init; }

    public List<HardwareTreeNode> Children { get; init; } = new();
}
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Converters;
using SystemMonitor.Presentation.ViewModels;
using Xunit;

namespace SystemMonitor.Tests;

public class TemperatureMonitoringFeatureTests
{
    [Fact]
    public void TemperatureTable_ShowsAvailableAndUnavailableReadingsInBothScales()
    {
        var table = new MetricsTableViewModel(
            new FakeMetricsSnapshotProvider(),
            "Temperature Table",
            MetricKind.Temperature);
        table.SetActive(true);

        var snapshot = new List<MetricReading>
        {
            new() { Id = "cpu.usage", Category = "CPU", Label = "Usage", Kind = MetricKind.Percentage, Unit = "%", Value = 40, IsAvailable = true },
            new() { Id = "temp.cpu.core", Category = "CPU", Label = "AMD Ryzen 3 3200G: Core #1", Kind = MetricKind.Temperature, Unit = "°C", Value = 0, IsAvailable = false },
            new() { Id = "temp.gpu.core.0", Category = "GPU", Label = "AMD Radeon Vega 8 Graphics: GPU Core", Kind = MetricKind.Temperature, Unit = "°C", Value = 65, IsAvailable = true },
            new() { Id = "temp.gpu.core.1", Category = "GPU", Label = "NVIDIA GeForce: Core", Kind = MetricKind.Temperature, Unit = "°C", Value = 60, IsAvailable = true },
            new() { Id = "temp.motherboard.sensor.1", Category = "MOTHERBOARD", Label = "Nuvoton NCT6797D: Temperature #1", Kind = MetricKind.Temperature, Unit = "°C", Value = 52, IsAvailable = true },
            new() { Id = "temp.disk.0", Category = "STORAGE", Label = "Samsung SSD 860 EVO M.2 250GB: Temperature", Kind = MetricKind.Temperature, Unit = "°C", Value = 0, IsAvailable = false }
        };

        table.ApplySnapshot(snapshot);

        Assert.Equal("Temperature Table", table.Title);
        Assert.Equal(5, table.TemperatureRows.Count);
        Assert.Equal("AMD Ryzen 3 3200G: Core #1", table.TemperatureRows[0].Metric);
        Assert.Equal("Unavailable", table.TemperatureRows[0].Celsius);
        Assert.Equal("Unavailable", table.TemperatureRows[0].Fahrenheit);
        Assert.Equal("AMD Radeon Vega 8 Graphics: GPU Core", table.TemperatureRows[1].Metric);
        Assert.Equal("65 °C", table.TemperatureRows[1].Celsius);
        Assert.Equal("149 °F", table.TemperatureRows[1].Fahrenheit);
        Assert.Equal("NVIDIA GeForce: Core", table.TemperatureRows[2].Metric);
        Assert.Equal("60 °C", table.TemperatureRows[2].Celsius);
        Assert.Equal("Nuvoton NCT6797D: Temperature #1", table.TemperatureRows[3].Metric);
        Assert.Equal("52 °C", table.TemperatureRows[3].Celsius);
        Assert.Equal("Samsung SSD 860 EVO M.2 250GB: Temperature", table.TemperatureRows[4].Metric);
        Assert.Equal("Unavailable", table.TemperatureRows[4].Celsius);
        Assert.Equal("Unavailable", table.TemperatureRows[4].Fahrenheit);
        Assert.False(table.NoTemperatureSensors);
        Assert.True(table.CpuPawnIoGuidanceVisible);

        table.ApplySnapshot(new List<MetricReading>
        {
            new() { Id = "temp.gpu.core.0", Category = "GPU", Label = "AMD Radeon Vega 8 Graphics: GPU Junction", Kind = MetricKind.Temperature, Unit = "°C", Value = 70, IsAvailable = true }
        });

        Assert.Single(table.TemperatureRows);
        Assert.Equal("AMD Radeon Vega 8 Graphics: GPU Junction", table.TemperatureRows[0].Metric);
        Assert.Equal("70 °C", table.TemperatureRows[0].Celsius);
        Assert.False(table.CpuPawnIoGuidanceVisible);
    }

    [Fact]
    public void TemperatureTable_ColorCodesAvailableTemperaturesOnly()
    {
        var converter = new TemperatureTableBrushConverter();
        var cool = new TemperatureTableRow("CPU", "Device: Core", "30 °C", "86 °F", 30);
        var hot = new TemperatureTableRow("CPU", "Device: Core", "95 °C", "203 °F", 95);
        var unavailable = new TemperatureTableRow("CPU", "Device: Core", "Unavailable", "Unavailable", null);

        var coolBrush = Assert.IsType<SolidColorBrush>(
            converter.Convert(cool, typeof(IBrush), null, CultureInfo.InvariantCulture));
        var hotBrush = Assert.IsType<SolidColorBrush>(
            converter.Convert(hot, typeof(IBrush), null, CultureInfo.InvariantCulture));

        Assert.NotEqual(coolBrush.Color, hotBrush.Color);
        Assert.Equal(Brushes.White, converter.Convert(unavailable, typeof(IBrush), null, CultureInfo.InvariantCulture));
    }

    private sealed class FakeMetricsSnapshotProvider : IMetricsSnapshotProvider
    {
        public IReadOnlyList<MetricReading> GetSnapshot() => new List<MetricReading>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;
using System.Collections.ObjectModel;


namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ICpuMonitorService _cpuMonitorService;
    private readonly IMemoryMonitorService _memoryMonitorService;
    private readonly IDiskMonitorService _diskMonitorService;
    private readonly INetworkMonitorService _networkMonitorService;
    private readonly ITemperatureMonitorService _temperatureMonitorService;
    private readonly DispatcherTimer _timer;

    private readonly Queue<double> _recentCpuSamples = new();
    private readonly Queue<double> _recentMemorySamples = new();
    private readonly Queue<double> _recentDiskSamples = new();
    private const int SmoothingWindow = 4;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double memoryUsage;

    [ObservableProperty]
    private string memoryUsageDisplay = string.Empty;

    [ObservableProperty]
    private double diskUsage;

    [ObservableProperty]
    private string diskUsageDisplay = string.Empty;

    [ObservableProperty]
    private string networkUsageDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> temperatureReadings = new();

    // Design-time only — used by the XAML previewer, never by the real running app
    public MainWindowViewModel()
    : this(new DesignTimeCpuMonitorService(), new DesignTimeMemoryMonitorService(),
           new DesignTimeDiskMonitorService(), new DesignTimeNetworkMonitorService(),
           new DesignTimeTemperatureMonitorService())
            {
            }

    public MainWindowViewModel(
        ICpuMonitorService cpuMonitorService,
        IMemoryMonitorService memoryMonitorService,
        IDiskMonitorService diskMonitorService,
        INetworkMonitorService networkMonitorService,
        ITemperatureMonitorService temperatureMonitorService)
    {
        _cpuMonitorService = cpuMonitorService;
        _memoryMonitorService = memoryMonitorService;
        _diskMonitorService = diskMonitorService;
        _networkMonitorService = networkMonitorService;
        _temperatureMonitorService = temperatureMonitorService;

        UpdateUsage();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _timer.Tick += (_, _) => UpdateUsage();
        _timer.Start();
    }

    private void UpdateUsage()
    {
        var cpuInfo = _cpuMonitorService.GetCurrentUsage();
        _recentCpuSamples.Enqueue(cpuInfo.UsagePercent);
        if (_recentCpuSamples.Count > SmoothingWindow)
            _recentCpuSamples.Dequeue();
        CpuUsage = _recentCpuSamples.Average();

        var memoryInfo = _memoryMonitorService.GetCurrentUsage();
        _recentMemorySamples.Enqueue(memoryInfo.UsagePercent);
        if (_recentMemorySamples.Count > SmoothingWindow)
            _recentMemorySamples.Dequeue();
        MemoryUsage = _recentMemorySamples.Average();

        var usedGB = memoryInfo.UsedMB / 1024.0;
        var totalGB = memoryInfo.TotalMB / 1024.0;
        MemoryUsageDisplay = $"Mem: {MemoryUsage:F0}% ({usedGB:F1} GB / {totalGB:F1} GB)";

        var diskInfo = _diskMonitorService.GetCurrentUsage();
        _recentDiskSamples.Enqueue(diskInfo.UsagePercent);
        if (_recentDiskSamples.Count > SmoothingWindow)
            _recentDiskSamples.Dequeue();
        DiskUsage = _recentDiskSamples.Average();

        DiskUsageDisplay =
            $"Disk Read: {diskInfo.ReadMBPerSec:F1} MB/s Disk Write: {diskInfo.WriteMBPerSec:F1} MB/s " +
            $"Disk Usage({diskInfo.DriveName}): {DiskUsage:F0}% ({diskInfo.UsedGB:F0} GB / {diskInfo.TotalGB:F0} GB) ";

        var networkInfo = _networkMonitorService.GetCurrentUsage();
        NetworkUsageDisplay = $"Net: ↓ {networkInfo.DownloadKBPerSec:F0} KB/s  ↑ {networkInfo.UploadKBPerSec:F0} KB/s";

        var rawTemperatureReadings = _temperatureMonitorService.GetCurrentUsage();
        var displayLines = new List<string>();
        foreach (var categoryGroup in rawTemperatureReadings.GroupBy(r => r.Category))
        {
            var availableReadings = categoryGroup.Where(r => r.IsAvailable).ToList();

            if (availableReadings.Count > 0)
            {
                displayLines.AddRange(availableReadings.Select(r =>
                    $"{categoryGroup.Key} - {r.SensorLabel} Temp: {r.TemperatureCelsius:F1}°C " +
                    $"(min {r.MinCelsius:F1} / max {r.MaxCelsius:F1} / avg {r.AverageCelsius:F1})"));
            }
            else
            {
                displayLines.Add($"{categoryGroup.Key} Temp: N/A");
            }
        }
    
    TemperatureReadings = new ObservableCollection<string>(displayLines);

        
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Application.Interfaces;

namespace SystemMonitor.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ICpuMonitorService _cpuMonitorService;
    private readonly IMemoryMonitorService _memoryMonitorService;
    private readonly DispatcherTimer _timer;

    private readonly Queue<double> _recentCpuSamples = new();
    private readonly Queue<double> _recentMemorySamples = new();
    private const int SmoothingWindow = 4;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double memoryUsage;

    [ObservableProperty]
    private string memoryUsageDisplay = string.Empty;

    // Design-time only — used by the XAML previewer, never by the real running app
    public MainWindowViewModel() : this(new DesignTimeCpuMonitorService(), new DesignTimeMemoryMonitorService())
    {
    }

    public MainWindowViewModel(ICpuMonitorService cpuMonitorService, IMemoryMonitorService memoryMonitorService)
    {
        _cpuMonitorService = cpuMonitorService;
        _memoryMonitorService = memoryMonitorService;

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
    }
}
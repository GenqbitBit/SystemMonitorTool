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
    private readonly DispatcherTimer _timer;

    private readonly Queue<double> _recentSamples = new();
    private const int SmoothingWindow = 4;

    [ObservableProperty]
    private double cpuUsage;

    // Design-time only — used by the XAML previewer, never by the real running app
    public MainWindowViewModel() : this(new DesignTimeCpuMonitorService())
    {
    }

    public MainWindowViewModel(ICpuMonitorService cpuMonitorService)
    {
        _cpuMonitorService = cpuMonitorService;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _timer.Tick += (_, _) => UpdateCpuUsage();
        _timer.Start();
    }

    private void UpdateCpuUsage()
    {
        var info = _cpuMonitorService.GetCurrentUsage();

        _recentSamples.Enqueue(info.UsagePercent);
        if (_recentSamples.Count > SmoothingWindow)
            _recentSamples.Dequeue();

        CpuUsage = _recentSamples.Average();
    }
}
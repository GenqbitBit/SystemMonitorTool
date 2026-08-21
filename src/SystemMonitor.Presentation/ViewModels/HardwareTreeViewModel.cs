using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

public partial class HardwareTreeViewModel : ObservableObject, IDisposable
{
    private IHardwareTreeProvider? _provider;
    private IEventLogService? _eventLog;
    private Thread? _pollThread;
    private readonly ManualResetEventSlim _stopSignal = new();
    private volatile bool _running;
    private volatile bool _disposed;
    private List<Domain.Models.HardwareTreeNode> _roots = new();

    private static readonly TimeSpan ValueRefreshInterval = TimeSpan.FromSeconds(2);

    public ObservableCollection<HardwareTreeNodeViewModel> Roots { get; } = new();

    public HardwareTreeViewModel()
    {
    }

    public HardwareTreeViewModel(IHardwareTreeProvider provider, IEventLogService eventLog)
    {
        Start(provider, eventLog);
    }

    public void Start(IHardwareTreeProvider provider, IEventLogService eventLog)
    {
        if (_pollThread is not null)
            return;

        _provider = provider;
        _eventLog = eventLog;
        _running = true;
        _pollThread = new Thread(PollLoop) { IsBackground = true };
        _pollThread.Start();
    }

    [RelayCommand]
    private void Refresh() => Discover();

    private void Discover()
    {
        var provider = _provider;
        if (provider is null)
            return;

        _roots = provider.DiscoverTree().ToList();
        var vms = _roots.Select(r => new HardwareTreeNodeViewModel(r)).ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !_running)
                return;

            Roots.Clear();
            foreach (var vm in vms) Roots.Add(vm);
        });
    }

    private void PollLoop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            Discover();
        }
        catch (Exception ex)
        {
            _eventLog?.LogEvent(EventType.Error, ex.Message);
        }

        while (_running)
        {
            try
            {
                if (sw.Elapsed >= ValueRefreshInterval)
                {
                    sw.Restart();
                    _provider?.RefreshValues(_roots);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (_disposed || !_running)
                            return;

                        foreach (var vm in Roots) vm.Refresh();
                    });
                }
            }
            catch (Exception ex)
            {
                _eventLog?.LogEvent(EventType.Error, ex.Message);
            }

            if (_stopSignal.Wait(ValueRefreshInterval))
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _running = false;
        _stopSignal.Set();
        _pollThread?.Join();
        _stopSignal.Dispose();
    }
}
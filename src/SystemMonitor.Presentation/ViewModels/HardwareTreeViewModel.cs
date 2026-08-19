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
    private readonly IHardwareTreeProvider _provider;
    private readonly IEventLogService _eventLog;
    private readonly Thread _pollThread;
    private volatile bool _running = true;
    private List<Domain.Models.HardwareTreeNode> _roots = new();

    private static readonly TimeSpan ValueRefreshInterval = TimeSpan.FromSeconds(2);

    public ObservableCollection<HardwareTreeNodeViewModel> Roots { get; } = new();

    public HardwareTreeViewModel(IHardwareTreeProvider provider, IEventLogService eventLog)
    {
        _provider = provider;
        _eventLog = eventLog;
        Discover(); // initial shape scan on startup

        _pollThread = new Thread(PollLoop) { IsBackground = true };
        _pollThread.Start();
    }

    [RelayCommand]
    private void Refresh() => Discover();

    private void Discover()
    {
        _roots = _provider.DiscoverTree().ToList();
        var vms = _roots.Select(r => new HardwareTreeNodeViewModel(r)).ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Roots.Clear();
            foreach (var vm in vms) Roots.Add(vm);
        });
    }

    private void PollLoop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (_running)
        {
            try
            {
                if (sw.Elapsed >= ValueRefreshInterval)
                {
                    sw.Restart();
                    _provider.RefreshValues(_roots);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var vm in Roots) vm.Refresh();
                    });
                }
            }
            catch (Exception ex)
            {
                _eventLog.LogEvent(EventType.Error, ex.Message);
            }

            Thread.Sleep(100);
        }
    }

    public void Dispose()
    {
        _running = false;
        _pollThread.Join(TimeSpan.FromSeconds(2));
    }
}
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;
using SystemMonitor.Presentation.Common;

namespace SystemMonitor.Presentation.ViewModels;

public partial class LogsPanelViewModel : ObservableObject
{
    private IEventLogService? _eventLog;

    [ObservableProperty]
    private ObservableCollection<EventLogEntry> entries = new();

    [ObservableProperty]
    private string? selectedTypeFilter; // null = all types

    public LogsPanelViewModel()
    {
    }

    public LogsPanelViewModel(IEventLogService eventLog)
    {
        Attach(eventLog);
    }

    public void Attach(IEventLogService eventLog)
    {
        _eventLog = eventLog;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task DeleteLogs()
    {
        if (_eventLog is null)
            return;

        await _eventLog.DeleteAllEventsAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var eventLog = _eventLog;
        if (eventLog is null)
            return;

        var results = await eventLog.GetEventsAsync(type: SelectedTypeFilter);

        Entries.SyncFrom(
            results,
            entry => (entry.Timestamp, entry.Type, entry.Message, entry.Metadata));
    }
}
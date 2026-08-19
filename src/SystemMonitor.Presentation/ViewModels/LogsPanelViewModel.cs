using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

public partial class LogsPanelViewModel : ObservableObject
{
    private readonly IEventLogService _eventLog;

    [ObservableProperty]
    private ObservableCollection<EventLogEntry> entries = new();

    [ObservableProperty]
    private string? selectedTypeFilter; // null = all types

    public LogsPanelViewModel(IEventLogService eventLog)
    {
        _eventLog = eventLog;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    private async Task LoadAsync()
    {
        var results = await _eventLog.GetEventsAsync(type: SelectedTypeFilter);

        Entries.Clear();
        foreach (var entry in results)
            Entries.Add(entry);
    }
}
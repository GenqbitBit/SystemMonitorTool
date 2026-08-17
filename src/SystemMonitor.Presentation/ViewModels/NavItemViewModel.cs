using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace SystemMonitor.Presentation.ViewModels;

public partial class NavItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string label = string.Empty;

    [ObservableProperty]
    private bool isActive;

    public IRelayCommand SelectCommand { get; }

    public NavItemViewModel(string label, Action<NavItemViewModel> onSelect)
    {
        Label = label;
        SelectCommand = new RelayCommand(() => onSelect(this));
    }
}
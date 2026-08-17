using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.ViewModels;

public partial class HardwareTreeNodeViewModel : ObservableObject
{
    private readonly HardwareTreeNode _node;

    public HardwareTreeNodeViewModel(HardwareTreeNode node)
    {
        _node = node;
        Children = new ObservableCollection<HardwareTreeNodeViewModel>(
            node.Children.Select(c => new HardwareTreeNodeViewModel(c)));

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].IsFirstChild = i == 0;
        }
    }

    public string Name => _node.Name;
    public bool IsLeaf => _node.Kind == HardwareTreeNodeKind.Sensor;
    public ObservableCollection<HardwareTreeNodeViewModel> Children { get; }

    [ObservableProperty] private string _displayValue = "N/A";
    [ObservableProperty] private bool _isAvailable;

    public bool IsFirstChild { get; set; }

    public void Refresh()
    {
        DisplayValue = _node.DisplayValue ?? "N/A";
        IsAvailable = _node.IsAvailable;
        foreach (var child in Children)
            child.Refresh();
    }
}
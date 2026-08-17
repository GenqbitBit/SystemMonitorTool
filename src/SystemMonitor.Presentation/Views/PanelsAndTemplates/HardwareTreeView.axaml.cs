using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SystemMonitor.Presentation.ViewModels;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class HardwareTreeView : UserControl
{
    public HardwareTreeView()
    {
        InitializeComponent();
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control) return;

        var item = control.FindAncestorOfType<TreeViewItem>();
        if (item is null) return;

        if (control.DataContext is HardwareTreeNodeViewModel { Children.Count: > 0 })
            item.IsExpanded = !item.IsExpanded;
    }
}
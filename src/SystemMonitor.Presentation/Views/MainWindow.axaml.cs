using Avalonia.Controls;
using SystemMonitor.Presentation.Services;
using SystemMonitor.Presentation.ViewModels;

namespace SystemMonitor.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly IPanelWindowService _panelWindowService;

    public MainWindow() : this(new PanelWindowService())
    {
    }

    public MainWindow(IPanelWindowService panelWindowService)
    {
        _panelWindowService = panelWindowService;
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.OpenPanelRequested += label => _panelWindowService.TogglePanel(label, this);
        };
    }
}
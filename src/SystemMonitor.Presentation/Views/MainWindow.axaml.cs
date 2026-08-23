using System;
using Avalonia.Controls;
using SystemMonitor.Presentation.Services;
using SystemMonitor.Presentation.ViewModels;

namespace SystemMonitor.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly IPanelWindowService _panelWindowService;
    private MainWindowViewModel? _subscribedViewModel;
    private Action<string>? _panelRequestHandler;

    public MainWindow() : this(new PanelWindowService())
    {
    }

    public MainWindow(IPanelWindowService panelWindowService)
    {
        _panelWindowService = panelWindowService;
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_subscribedViewModel is not null && _panelRequestHandler is not null)
                _subscribedViewModel.OpenPanelRequested -= _panelRequestHandler;

            _subscribedViewModel = null;
            _panelRequestHandler = null;

            if (DataContext is MainWindowViewModel vm)
            {
                _subscribedViewModel = vm;
                _panelRequestHandler = label => _panelWindowService.TogglePanel(label, this);
                vm.OpenPanelRequested += _panelRequestHandler;
            }
        };

        Closed += (_, _) =>
        {
            BackgroundImage.Dispose();
            _panelWindowService.CloseAllPanels();

            if (_subscribedViewModel is not null && _panelRequestHandler is not null)
                _subscribedViewModel.OpenPanelRequested -= _panelRequestHandler;

            _subscribedViewModel = null;
            _panelRequestHandler = null;
        };
    }
}
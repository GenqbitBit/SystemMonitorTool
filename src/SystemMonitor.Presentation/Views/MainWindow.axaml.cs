using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SystemMonitor.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            GraphDebugBorder.BorderBrush = Brushes.Red;
            GraphDebugBorder.BorderThickness = new Thickness(1);
        }
    }
}
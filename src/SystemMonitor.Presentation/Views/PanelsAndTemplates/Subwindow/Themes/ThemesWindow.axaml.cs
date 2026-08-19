using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SystemMonitor.Presentation.Views.Subwindows.Themes;

public partial class ThemesWindow : Window
{
    public ThemesWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
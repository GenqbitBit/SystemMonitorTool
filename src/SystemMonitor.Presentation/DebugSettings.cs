using Avalonia.Controls;
using Avalonia.Media;

namespace SystemMonitor.Presentation;

/// <summary>
/// Single app-wide switch for design-time debug borders. Any Border's
/// BorderBrush can bind to DebugSettings.BorderBrush via {x:Static} —
/// resolves to LimeGreen when on, Transparent when off. Defaults to on
/// only inside the XAML previewer. Flip ShowDebugBorders here to force
/// debug borders on/off everywhere at once, including at runtime.
/// </summary>
public static class DebugSettings
{
    public static bool ShowDebugBorders { get; set; } = Design.IsDesignMode;

    public static IBrush BorderBrush => ShowDebugBorders ? Brushes.LimeGreen : Brushes.Transparent;
}
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace SystemMonitor.Presentation.Debugging;

public sealed class DebugSettings : INotifyPropertyChanged
{
    public static DebugSettings Instance { get; } = new();

    private DebugSettings()
    {
        // Design-time: on by default. Runtime: off by default.
        _showDebugBorders = Design.IsDesignMode;
    }

    private bool _showDebugBorders;
    public bool ShowDebugBorders
    {
        get => _showDebugBorders;
        set
        {
            if (_showDebugBorders == value) return;
            _showDebugBorders = value;
            OnPropertyChanged();
        }
    }

    // Centralized look-and-feel for debug borders — change once, affects everywhere.
    public IBrush ActiveBrush { get; set; } = Brushes.Gray;
    public double ActiveThickness { get; set; } = 1.0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
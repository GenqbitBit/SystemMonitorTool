using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System.ComponentModel;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class AsciiArtPanel : UserControl
{
    private DispatcherTimer? _spinTimer;
    private double _angle;
    private AsciiArtPanelViewModel? _vm;

    public AsciiArtPanel()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            this.AttachedToVisualTree += (_, _) => StartSpin();
            this.DetachedFromVisualTree += (_, _) => StopSpin();
            this.DataContextChanged += OnDataContextChanged;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as AsciiArtPanelViewModel;

        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AsciiArtPanelViewModel.IsPlaying) || _vm is null)
            return;

        if (_vm.IsPlaying) _spinTimer?.Start();
        else _spinTimer?.Stop();
    }

    private void StartSpin()
    {
        if (ArtVisual.RenderTransform is not ScaleTransform)
            return;

        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += (_, _) =>
        {
            _angle = (_angle + 1.0) % 360.0;
            double radians = _angle * Math.PI / 180.0;

            if (ArtVisual.RenderTransform is ScaleTransform t)
            {
                t.ScaleX = Math.Cos(radians);
                ArtVisual.Opacity = 0.35 + 0.65 * Math.Abs(Math.Cos(radians));
            }
        };

        if (_vm is null || _vm.IsPlaying)
            _spinTimer.Start();
    }

    private void StopSpin()
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _spinTimer?.Stop();
        _spinTimer = null;
    }
}
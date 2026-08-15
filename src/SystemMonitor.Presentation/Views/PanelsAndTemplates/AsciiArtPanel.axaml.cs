using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class AsciiArtPanel : UserControl
{
    private DispatcherTimer? _spinTimer;
    private double _angle;

    public AsciiArtPanel()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            this.AttachedToVisualTree += (_, _) => StartSpin();
            this.DetachedFromVisualTree += (_, _) => StopSpin();
        }
    }

    private void StartSpin()
    {
        if (ArtVisual.RenderTransform is not RotateTransform)
            return;

        _spinTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _spinTimer.Tick += (_, _) =>
        {
            _angle = (_angle + 1.0) % 360.0; // ~6s per full rotation at 60fps
            if (ArtVisual.RenderTransform is RotateTransform t)
                t.Angle = _angle;
        };
        _spinTimer.Start();
    }

    private void StopSpin()
    {
        _spinTimer?.Stop();
        _spinTimer = null;
    }
}
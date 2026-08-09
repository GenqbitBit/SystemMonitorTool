using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SystemMonitor.Presentation.Debugging;

public static class DebugBorder
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Border, bool>("IsEnabled", typeof(DebugBorder));

    public static bool GetIsEnabled(Border border) => border.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Border border, bool value) => border.SetValue(IsEnabledProperty, value);

    static DebugBorder()
    {
        IsEnabledProperty.Changed.AddClassHandler<Border>(OnIsEnabledChanged);
    }

    private static void OnIsEnabledChanged(Border border, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        // Preserve whatever thickness/brush the user set as the "feature" state.
        var featureBrush = border.BorderBrush;
        var featureThickness = border.BorderThickness;

        void Apply()
        {
            var debugOn = DebugSettings.Instance.ShowDebugBorders;
            border.BorderBrush = debugOn ? DebugSettings.Instance.ActiveBrush : featureBrush;
            border.BorderThickness = debugOn
                ? new Thickness(DebugSettings.Instance.ActiveThickness)
                : featureThickness;
        }

        Apply();

        void Handler(object? _, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(DebugSettings.ShowDebugBorders)
                or nameof(DebugSettings.ActiveBrush)
                or nameof(DebugSettings.ActiveThickness))
            {
                Apply();
            }
        }

        DebugSettings.Instance.PropertyChanged += Handler;

        // Unsubscribe when the border leaves the tree, to avoid leaking.
        border.DetachedFromVisualTree += (_, _) => DebugSettings.Instance.PropertyChanged -= Handler;
    }
}
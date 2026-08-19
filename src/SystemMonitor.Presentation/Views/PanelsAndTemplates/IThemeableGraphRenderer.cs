using Avalonia.Media;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Optional add-on to IGraphContentRenderer. Renderers that implement this
/// get their colors pushed in by MetricGraphView whenever the active theme
/// changes — IGraphContentRenderer itself stays untouched.
/// </summary>
public interface IThemeableGraphRenderer
{
    void ApplyPalette(Color primary, Color secondary);
}
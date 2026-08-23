using Avalonia.Controls;

namespace SystemMonitor.Presentation.Services;

public interface IPanelWindowService
{
    void TogglePanel(string label, Window owner);
    void CloseAllPanels();
}
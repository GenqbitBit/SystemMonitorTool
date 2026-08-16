using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class CpuProcessesView : UserControl
{
    public static readonly StyledProperty<IEnumerable<ProcessInfo>?> ProcessesProperty =
        AvaloniaProperty.Register<CpuProcessesView, IEnumerable<ProcessInfo>?>(nameof(Processes));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CpuProcessesView, string>(nameof(Title), "Top CPU");

    public IEnumerable<ProcessInfo>? Processes
    {
        get => GetValue(ProcessesProperty);
        set => SetValue(ProcessesProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CpuProcessesView()
    {
        InitializeComponent();
    }
}
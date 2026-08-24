using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class MemoryProcessesView : UserControl
{
    public static readonly StyledProperty<IEnumerable<ProcessInfo>?> ProcessesProperty =
        AvaloniaProperty.Register<MemoryProcessesView, IEnumerable<ProcessInfo>?>(nameof(Processes));

    public static readonly StyledProperty<string> TitleProperty =
    AvaloniaProperty.Register<MemoryProcessesView, string>(nameof(Title), "Top Mem");

public static readonly StyledProperty<string> TitleSuffixProperty =
    AvaloniaProperty.Register<MemoryProcessesView, string>(nameof(TitleSuffix), "10 seconds");

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

    public string TitleSuffix
    {
        get => GetValue(TitleSuffixProperty);
        set => SetValue(TitleSuffixProperty, value);
    }

    public MemoryProcessesView()
    {
        InitializeComponent();
    }
}
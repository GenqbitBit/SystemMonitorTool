using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class ProcessesView : UserControl
{
    public static readonly StyledProperty<IEnumerable<ProcessInfo>?> ProcessesProperty =
        AvaloniaProperty.Register<ProcessesView, IEnumerable<ProcessInfo>?>(nameof(Processes));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProcessesView, string>(nameof(Title), "Top Processes Interval:10 seconds");

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

    public ProcessesView()
    {
        InitializeComponent();
    }
}
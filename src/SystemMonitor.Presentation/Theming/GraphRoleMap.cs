using System.Collections.Generic;
using Avalonia.Media;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Presentation.Theming;

public enum GraphRole
{
    CpuBar, CpuArea,
    MemoryBar, MemoryArea,
    DiskBar, DiskRead, DiskWrite,
    NetworkDownloadBar, NetworkUploadBar, NetworkArea,
    GpuArea,
}

public static class GraphRoleMap
{
    public static (Color Primary, Color Secondary) Resolve(GraphRole role, ThemeDefinition theme)
    {
        var r = theme.Roles;
        Color c(ThemeColor tc) => ThemeResourceApplier.ToColor(tc);

        return role switch
        {
            GraphRole.CpuBar => (c(r.Accent1), c(r.Critical)),
            GraphRole.CpuArea => (c(r.Positive), c(r.Warning)),
            GraphRole.MemoryBar => (c(r.Positive), c(r.Critical)),
            GraphRole.MemoryArea => (c(r.Accent3), c(r.Accent1)),
            GraphRole.DiskBar => (c(r.Positive), c(r.Warning)),
            GraphRole.DiskRead => (c(r.Accent3), c(r.Accent1)),
            GraphRole.DiskWrite => (c(r.Warning), c(r.Accent2)),
            GraphRole.NetworkDownloadBar => (c(r.Accent3), c(r.Accent4)),
            GraphRole.NetworkUploadBar => (c(r.Accent3), c(r.Accent4)),
            GraphRole.NetworkArea => (c(r.Accent1), c(r.Accent3)),
            GraphRole.GpuArea => (c(r.Accent1), c(r.Warning)),
            _ => (c(r.Accent1), c(r.Accent2)),
        };
    }
}
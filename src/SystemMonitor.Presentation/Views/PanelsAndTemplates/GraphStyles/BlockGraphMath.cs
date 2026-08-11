using System;
using System.Collections.Generic;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public static class BlockGraphMath
{
    public static double InterpolateYAt(IReadOnlyList<(double X, double Y)> points, double x)
    {
        if (x <= points[0].X) return points[0].Y;
        if (x >= points[^1].X) return points[^1].Y;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i]; var b = points[i + 1];
            if (x >= a.X && x <= b.X)
            {
                if (b.X - a.X < 0.0001) return a.Y;
                var t = (x - a.X) / (b.X - a.X);
                return a.Y + t * (b.Y - a.Y);
            }
        }
        return points[^1].Y;
    }
}
using System.Collections.Generic;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public static class BlockGraphMath
{
    public static double InterpolateYAt(
        IReadOnlyList<(double X, double Y)> points,
        double x)
    {
        if (x <= points[0].X) return points[0].Y;
        if (x >= points[^1].X) return points[^1].Y;

        var low = 0;
        var high = points.Count - 1;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            if (points[middle].X <= x)
                low = middle;
            else
                high = middle;
        }

        var left = points[low];
        var right = points[high];
        if (right.X - left.X < 0.0001) return left.Y;

        var t = (x - left.X) / (right.X - left.X);
        return left.Y + t * (right.Y - left.Y);
    }
}
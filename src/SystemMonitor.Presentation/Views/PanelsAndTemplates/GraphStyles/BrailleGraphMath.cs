using System;
using System.Collections.Generic;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Pure math for braille-dot graph rendering (the technique btop uses for
/// its smooth "dot" graphs): each Unicode braille character (U+2800–28FF)
/// packs an independent 2-column x 4-row grid of sub-dots into one glyph,
/// so a curve rendered this way has 8x the positional resolution of one
/// character per sample point. Avalonia-free, unit-testable like
/// MetricGraphMath/DotFillMath. Deliberately self-contained (its own
/// interpolation helper, not shared with DotFillMath) so it has zero
/// dependency on other renderer-support files.
/// </summary>
public static class BrailleGraphMath
{
    private const int SubCols = 2;
    private const int SubRows = 4;

    // Standard braille dot bit layout: dots 1/2/3/7 in the left sub-column
    // (top to bottom), dots 4/5/6/8 in the right sub-column.
    private static readonly int[,] DotBits =
    {
        { 0x01, 0x08 },
        { 0x02, 0x10 },
        { 0x04, 0x20 },
        { 0x40, 0x80 },
    };

    public static IReadOnlyList<(double CellX, double CellY, int Mask)> ComputeBrailleCells(
        IReadOnlyList<(double X, double Y)> curvePoints, double width, double height,
        double cellWidth, double cellHeight)
    {
        if (curvePoints.Count < 2 || width <= 0 || height <= 0 || cellWidth <= 0 || cellHeight <= 0)
            return Array.Empty<(double, double, int)>();

        var cells = new List<(double, double, int)>();
        var columnCount = (int)Math.Ceiling(width / cellWidth);
        var rowCount = (int)Math.Ceiling(height / cellHeight);
        var subDotWidth = cellWidth / SubCols;
        var subDotHeight = cellHeight / SubRows;

        for (int col = 0; col < columnCount; col++)
        {
            var cellLeft = col * cellWidth;
            if (cellLeft > width) break;

            var y0 = InterpolateY(curvePoints, Math.Min(cellLeft + subDotWidth * 0.5, width));
            var y1 = InterpolateY(curvePoints, Math.Min(cellLeft + subDotWidth * 1.5, width));

            for (int row = 0; row < rowCount; row++)
            {
                var cellTop = row * cellHeight;
                if (cellTop > height) break;

                var mask = 0;
                for (int subRow = 0; subRow < SubRows; subRow++)
                {
                    var subY = cellTop + subDotHeight * (subRow + 0.5);
                    if (Math.Abs(subY - y0) <= subDotHeight * 0.5) mask |= DotBits[subRow, 0];
                    if (Math.Abs(subY - y1) <= subDotHeight * 0.5) mask |= DotBits[subRow, 1];
                }

                if (mask != 0)
                    cells.Add((cellLeft, cellTop, mask));
            }
        }

        return cells;
    }

    public static IReadOnlyList<(double CellX, double CellY, int Mask)> ComputeBrailleAreaCells(
        IReadOnlyList<(double X, double Y)> curvePoints, double width, double height,
        double cellWidth, double cellHeight)
    {
        if (curvePoints.Count < 2 || width <= 0 || height <= 0 || cellWidth <= 0 || cellHeight <= 0)
            return Array.Empty<(double, double, int)>();

        var cells = new List<(double, double, int)>();
        var columnCount = (int)Math.Ceiling(width / cellWidth);
        var rowCount = (int)Math.Ceiling(height / cellHeight);
        var subDotWidth = cellWidth / SubCols;
        var subDotHeight = cellHeight / SubRows;

        for (int col = 0; col < columnCount; col++)
        {
            var cellLeft = col * cellWidth;
            if (cellLeft > width) break;

            var y0 = InterpolateY(curvePoints, Math.Min(cellLeft + subDotWidth * 0.5, width));
            var y1 = InterpolateY(curvePoints, Math.Min(cellLeft + subDotWidth * 1.5, width));

            for (int row = 0; row < rowCount; row++)
            {
                var cellTop = row * cellHeight;
                if (cellTop > height) break;

                // Unlike ComputeBrailleCells (which only lights a sub-dot when
                // it sits ON the curve), this lights every sub-dot BELOW the
                // curve too — i.e. between the curve and the plot floor —
                // which is what actually produces a filled area.
                var mask = 0;
                for (int subRow = 0; subRow < SubRows; subRow++)
                {
                    var subY = cellTop + subDotHeight * (subRow + 0.5);
                    if (subY >= y0) mask |= DotBits[subRow, 0];
                    if (subY >= y1) mask |= DotBits[subRow, 1];
                }

                if (mask != 0)
                    cells.Add((cellLeft, cellTop, mask));
            }
        }

        return cells;
    }

    public static char ToBrailleChar(int mask) => (char)(0x2800 + (mask & 0xFF));

    private static double InterpolateY(IReadOnlyList<(double X, double Y)> points, double x)
    {
        if (x <= points[0].X) return points[0].Y;
        if (x >= points[^1].X) return points[^1].Y;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
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
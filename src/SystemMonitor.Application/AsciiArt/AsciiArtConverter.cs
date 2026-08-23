using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Application.AsciiArt;

public sealed class AsciiArtConverter : IAsciiArtConverter
{
    private const int AnalysisScale = 2;

    public AsciiCell[,] Convert(IPixelSource source, int columns, int rows)
    {
        int analysisCols = Math.Max(columns * AnalysisScale, 1);
        int analysisRows = Math.Max(rows * AnalysisScale, 1);

        var analysis = new BlockAnalysis[analysisRows, analysisCols];

        double blockW = (double)source.Width / analysisCols;
        double blockH = (double)source.Height / analysisRows;

        for (int row = 0; row < analysisRows; row++)
        {
            for (int col = 0; col < analysisCols; col++)
            {
                int startX = (int)(col * blockW);
                int startY = (int)(row * blockH);
                int width = Math.Max(1, (int)Math.Ceiling(blockW));
                int height = Math.Max(1, (int)Math.Ceiling(blockH));

                analysis[row, col] = SampleBlock(source, startX, startY, width, height);
            }
        }

        var cells = new AsciiCell[rows, columns];

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var region = AggregateRegion(analysis, row * AnalysisScale, col * AnalysisScale, AnalysisScale, AnalysisScale);

                if (!region.HasContent)
                {
                    cells[row, col] = new AsciiCell(' ', 0);
                    continue;
                }

                var glyph = AsciiGlyphRamp.SelectGlyph(
                    region.Brightness,
                    region.EdgeStrength,
                    region.Density,
                    region.Contrast);
                var tier = AsciiGlyphRamp.ResolveTier(
                    region.Brightness,
                    region.EdgeStrength,
                    region.Density,
                    region.Contrast);

                cells[row, col] = new AsciiCell(glyph, tier);
            }
        }

        return cells;
    }

    private static BlockAnalysis AggregateRegion(
        BlockAnalysis[,] analysis,
        int startRow,
        int startCol,
        int regionHeight,
        int regionWidth)
    {
        double brightnessSum = 0;
        double edgeStrengthSum = 0;
        double densitySum = 0;
        double contrastSum = 0;
        int count = 0;

        int endRow = Math.Min(analysis.GetLength(0), startRow + regionHeight);
        int endCol = Math.Min(analysis.GetLength(1), startCol + regionWidth);

        for (int row = startRow; row < endRow; row++)
        {
            for (int col = startCol; col < endCol; col++)
            {
                var sample = analysis[row, col];
                if (!sample.HasContent)
                    continue;

                brightnessSum += sample.Brightness;
                edgeStrengthSum += sample.EdgeStrength;
                densitySum += sample.Density;
                contrastSum += sample.Contrast;
                count++;
            }
        }

        if (count == 0)
            return new BlockAnalysis(false, 0, 0, 0, 0);

        return new BlockAnalysis(
            true,
            brightnessSum / count,
            contrastSum / count,
            edgeStrengthSum / count,
            densitySum / count);
    }

    private static BlockAnalysis SampleBlock(IPixelSource source, int startX, int startY, int w, int h)
    {
        w = Math.Max(1, w);
        h = Math.Max(1, h);

        long sumR = 0, sumG = 0, sumB = 0;
        double luminanceSum = 0;
        double varianceSum = 0;
        double edgeSum = 0;
        int count = 0;

        int endX = Math.Min(source.Width, startX + w);
        int endY = Math.Min(source.Height, startY + h);

        var luminance = new double[endY - startY, endX - startX];

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var (r, g, b, a) = source.GetPixel(x, y);
                if (a == 0) continue;

                sumR += r; sumG += g; sumB += b;

                double pixelLuminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
                luminanceSum += pixelLuminance;
                luminance[y - startY, x - startX] = pixelLuminance;
                count++;
            }
        }

        if (count == 0)
            return new BlockAnalysis(false, 0, 0, 0, 0);

        double avgLuminance = luminanceSum / count;
        double density = (double)count / (w * h);

        for (int y = 0; y < luminance.GetLength(0); y++)
        {
            for (int x = 0; x < luminance.GetLength(1); x++)
            {
                double delta = luminance[y, x] - avgLuminance;
                varianceSum += delta * delta;

                if (x + 1 < luminance.GetLength(1))
                    edgeSum += Math.Abs(luminance[y, x] - luminance[y, x + 1]);

                if (y + 1 < luminance.GetLength(0))
                    edgeSum += Math.Abs(luminance[y, x] - luminance[y + 1, x]);
            }
        }

        double edgeStrength = edgeSum / Math.Max(1, (luminance.GetLength(0) * luminance.GetLength(1) * 2));
        double contrast = Math.Sqrt(varianceSum / Math.Max(1, count));

        byte avgR = (byte)(sumR / count);
        byte avgG = (byte)(sumG / count);
        byte avgB = (byte)(sumB / count);
        double brightness = (0.299 * avgR + 0.587 * avgG + 0.114 * avgB) / 255.0;

        return new BlockAnalysis(true, brightness, contrast, edgeStrength, density);
    }

    private readonly record struct BlockAnalysis(
        bool HasContent,
        double Brightness,
        double Contrast,
        double EdgeStrength,
        double Density);
}
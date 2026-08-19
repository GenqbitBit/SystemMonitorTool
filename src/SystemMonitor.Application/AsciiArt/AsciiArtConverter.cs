using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Application.AsciiArt;

public sealed class AsciiArtConverter : IAsciiArtConverter
{
    public AsciiCell[,] Convert(IPixelSource source, int columns, int rows)
    {
        var cells = new AsciiCell[rows, columns];

        double blockW = (double)source.Width / columns;
        double blockH = (double)source.Height / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var (r, g, b, brightness, hasContent) = SampleBlock(
                    source,
                    (int)(col * blockW), (int)(row * blockH),
                    (int)blockW, (int)blockH);

                if (!hasContent)
                {
                    cells[row, col] = new AsciiCell(' ', 0, 0, 0, 0);
                    continue;
                }

                int tier = (int)Math.Round(brightness * (AsciiGlyphRamp.Tiers.Length - 1));
                tier = Math.Clamp(tier, 0, AsciiGlyphRamp.Tiers.Length - 1);

                cells[row, col] = new AsciiCell(AsciiGlyphRamp.Tiers[tier], r, g, b, tier);
            }
        }

        return cells;
    }

    private static (byte R, byte G, byte B, double Brightness, bool HasContent) SampleBlock(
        IPixelSource source, int startX, int startY, int w, int h)
    {
        w = Math.Max(1, w);
        h = Math.Max(1, h);

        long sumR = 0, sumG = 0, sumB = 0;
        int count = 0;

        int endX = Math.Min(source.Width, startX + w);
        int endY = Math.Min(source.Height, startY + h);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var (r, g, b, a) = source.GetPixel(x, y);
                if (a == 0) continue;
                sumR += r; sumG += g; sumB += b;
                count++;
            }
        }

        if (count == 0) return (0, 0, 0, 0, false);

        byte avgR = (byte)(sumR / count);
        byte avgG = (byte)(sumG / count);
        byte avgB = (byte)(sumB / count);

        double brightness = (0.299 * avgR + 0.587 * avgG + 0.114 * avgB) / 255.0;

        return (avgR, avgG, avgB, brightness, true);
    }
}
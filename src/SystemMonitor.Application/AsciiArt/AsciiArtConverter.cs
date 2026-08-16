using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Application.AsciiArt;

public sealed class AsciiArtConverter : IAsciiArtConverter
{
    // Ordered dark -> light. Tune density/length to taste.
    private const string Ramp = " .:-=+*#%@";

    public AsciiCell[,] Convert(IPixelSource source, int columns, int rows)
    {
        var cells = new AsciiCell[rows, columns];

        double blockW = (double)source.Width / columns;
        double blockH = (double)source.Height / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var (r, g, b, brightness) = SampleBlock(
                    source,
                    (int)(col * blockW), (int)(row * blockH),
                    (int)blockW, (int)blockH);

                char glyph = MapBrightnessToGlyph(brightness);
                cells[row, col] = new AsciiCell(glyph, r, g, b);
            }
        }

        return cells;
    }

    private static (byte R, byte G, byte B, double Brightness) SampleBlock(
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
                if (a == 0) continue; // skip fully transparent pixels
                sumR += r; sumG += g; sumB += b;
                count++;
            }
        }

        if (count == 0) return (0, 0, 0, 0);

        byte avgR = (byte)(sumR / count);
        byte avgG = (byte)(sumG / count);
        byte avgB = (byte)(sumB / count);

        // Perceptual luminance, normalized 0..1
        double brightness = (0.299 * avgR + 0.587 * avgG + 0.114 * avgB) / 255.0;

        return (avgR, avgG, avgB, brightness);
    }

    private static char MapBrightnessToGlyph(double brightness)
    {
        int index = (int)(brightness * (Ramp.Length - 1));
        index = Math.Clamp(index, 0, Ramp.Length - 1);
        return Ramp[index];
    }
}
using SystemMonitor.Application.AsciiArt;
using SystemMonitor.Domain.AsciiArt;
using SystemMonitor.Presentation.Views.PanelsAndTemplates;
using Xunit;

namespace SystemMonitor.Tests;

public class AsciiArtGlintTests
{
    [Fact]
    public void AsciiCell_UsesGlyphAndTierOnly_ForThemeDrivenRendering()
    {
        var cell = new AsciiCell('A', 2);

        Assert.Equal('A', cell.Glyph);
        Assert.Equal(2, cell.Tier);
    }

    [Fact]
    public void Converter_SelectsMultipleGlyphs_ForStructuredImageRegions()
    {
        var source = new TestPixelSource(new[]
        {
            // dark background with bright highlight and edge-like structure
            new (byte, byte, byte, byte)[] { (0, 0, 0, 255), (0, 0, 0, 255), (0, 0, 0, 255), (0, 0, 0, 255) },
            new (byte, byte, byte, byte)[] { (0, 0, 0, 255), (255, 255, 255, 255), (255, 255, 255, 255), (0, 0, 0, 255) },
            new (byte, byte, byte, byte)[] { (0, 0, 0, 255), (255, 255, 255, 255), (255, 0, 0, 255), (0, 0, 0, 255) },
            new (byte, byte, byte, byte)[] { (0, 0, 0, 255), (0, 0, 0, 255), (0, 0, 0, 255), (0, 0, 0, 255) },
        });

        var converter = new AsciiArtConverter();
        var art = converter.Convert(source, 4, 4);

        var glyphs = new HashSet<char>();
        foreach (var cell in art)
        {
            glyphs.Add(cell.Glyph);
        }

        Assert.True(glyphs.Count > 1, $"Expected multiple glyph choices, but got: {string.Join(",", glyphs)}");
    }

    [Fact]
    public void ComputeGlintFactor_VariesByRowAndColumn_AndStaysWithinSubtleRange()
    {
        const double elapsedMs = 1500;

        var row0 = AsciiArtVisual.ComputeGlintFactor(0, 0, elapsedMs);
        var row1 = AsciiArtVisual.ComputeGlintFactor(1, 0, elapsedMs);
        var col1 = AsciiArtVisual.ComputeGlintFactor(0, 1, elapsedMs);

        Assert.NotEqual(row0, row1);
        Assert.NotEqual(row0, col1);
        Assert.InRange(row0, 0.85, 1.25);
        Assert.InRange(row1, 0.85, 1.25);
        Assert.InRange(col1, 0.85, 1.25);
    }

    private sealed class TestPixelSource : IPixelSource
    {
        private readonly (byte R, byte G, byte B, byte A)[] _pixels;

        public TestPixelSource((byte R, byte G, byte B, byte A)[][] rows)
        {
            Height = rows.Length;
            Width = rows[0].Length;
            _pixels = new (byte R, byte G, byte B, byte A)[Height * Width];

            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    _pixels[y * Width + x] = rows[y][x];
                }
            }
        }

        public int Width { get; }
        public int Height { get; }

        public (byte R, byte G, byte B, byte A) GetPixel(int x, int y)
        {
            return _pixels[y * Width + x];
        }
    }
}

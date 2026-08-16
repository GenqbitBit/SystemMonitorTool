// Domain/AsciiArt/IAsciiArtConverter.cs
namespace SystemMonitor.Domain.AsciiArt;

public interface IAsciiArtConverter
{
    AsciiCell[,] Convert(IPixelSource source, int columns, int rows);
}

// Small abstraction so this layer never touches Avalonia.Bitmap directly.
public interface IPixelSource
{
    int Width { get; }
    int Height { get; }
    (byte R, byte G, byte B, byte A) GetPixel(int x, int y);
}
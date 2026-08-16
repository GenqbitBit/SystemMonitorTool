using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public sealed class WriteableBitmapPixelSource : IPixelSource, IDisposable
{
    private readonly byte[] _pixels;
    private readonly int _rowBytes;
    private readonly bool _isRgba;

    public int Width { get; }
    public int Height { get; }

    public WriteableBitmapPixelSource(WriteableBitmap bitmap)
    {
        Width = bitmap.PixelSize.Width;
        Height = bitmap.PixelSize.Height;

        using var fb = bitmap.Lock();
        _rowBytes = fb.RowBytes;
        _isRgba = fb.Format == PixelFormat.Rgba8888;

        _pixels = new byte[fb.RowBytes * Height];
        System.Runtime.InteropServices.Marshal.Copy(fb.Address, _pixels, 0, _pixels.Length);
    }

    public (byte R, byte G, byte B, byte A) GetPixel(int x, int y)
    {
        int offset = y * _rowBytes + x * 4;

        return _isRgba
            ? (_pixels[offset], _pixels[offset + 1], _pixels[offset + 2], _pixels[offset + 3])       // R,G,B,A
            : (_pixels[offset + 2], _pixels[offset + 1], _pixels[offset], _pixels[offset + 3]);      // B,G,R -> R,G,B
    }

    public void Dispose() { /* nothing to dispose — no unmanaged handles held */ }
}
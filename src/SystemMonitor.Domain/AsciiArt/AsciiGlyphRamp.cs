// Domain/AsciiArt/AsciiGlyphRamp.cs
namespace SystemMonitor.Domain.AsciiArt;

public static class AsciiGlyphRamp
{
    // light -> dark. Hard-quantized tiers, no smooth gradient/dithering.
    public const string Tiers = "%$#@&A";
}
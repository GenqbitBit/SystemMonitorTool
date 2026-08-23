namespace SystemMonitor.Domain.AsciiArt;

public static class AsciiGlyphRamp
{
    // Broader ramp + detail-aware selection so structure is encoded in the glyphs themselves,
    // not just in image colors. The ramp balances shadow, texture, and edge detail.
    public const string Tiers = " .,:;=+*#%@";

    public static char SelectGlyph(double brightness, double edgeStrength, double density, double contrast = 0)
    {
        if (density < 0.06)
            return ' ';

        if (brightness < 0.08)
            return '@';

        if (brightness < 0.16)
            return '%';

        if (brightness < 0.28)
            return '#';

        if (brightness < 0.40)
            return '*';

        if (brightness < 0.52)
            return '+';

        if (brightness < 0.66)
            return '=';

        if (brightness < 0.74)
            return ';';

        if (brightness < 0.84)
            return ':';

        if (contrast > 0.18 || edgeStrength > 0.24)
            return ',';

        return '.';
    }

    public static int ResolveTier(double brightness, double edgeStrength, double density, double contrast = 0)
    {
        if (density < 0.06)
            return 0;

        var index = (int)Math.Round(brightness * (Tiers.Length - 1));
        index = Math.Clamp(index, 0, Tiers.Length - 1);

        if (contrast > 0.22 && brightness > 0.25 && brightness < 0.78)
            index = Math.Clamp(index + 1, 0, Tiers.Length - 1);

        if (edgeStrength > 0.35 && brightness < 0.30)
            index = Math.Clamp(index - 1, 0, Tiers.Length - 1);

        if (brightness > 0.90 && edgeStrength < 0.12)
            index = Math.Clamp(index - 1, 0, Tiers.Length - 1);

        return index;
    }
}
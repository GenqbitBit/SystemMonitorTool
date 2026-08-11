using Avalonia.Media;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

/// <summary>
/// Pure color interpolation/banding helpers shared by area-fill renderers.
/// </summary>
public static class GraphColorMath
{
    public static Color Lerp(Color a, Color b, double t)
    {
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        return new Color(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>
    /// Quantizes t (0..1, LOCAL to whatever span the caller passes in — not
    /// necessarily the whole plot height) into discrete bands.
    /// </summary>
    public static Color Band(Color a, Color b, double t, int bandCount)
    {
        if (bandCount < 1) bandCount = 1;
        t = t < 0 ? 0 : t > 1 ? 1 : t;

        var bandIndex = (int)(t * bandCount);
        if (bandIndex >= bandCount) bandIndex = bandCount - 1;

        var bandT = (bandIndex + 0.5) / bandCount;
        return Lerp(a, b, bandT);
    }
}
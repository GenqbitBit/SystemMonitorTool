namespace SystemMonitor.Domain.Models.Theming;

public readonly record struct ThemeColor(byte A, byte R, byte G, byte B)
{
    public static ThemeColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static ThemeColor FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            return FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));

        if (hex.Length == 8)
            return new ThemeColor(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16));

        throw new FormatException($"Unrecognized color hex '{hex}'.");
    }
}
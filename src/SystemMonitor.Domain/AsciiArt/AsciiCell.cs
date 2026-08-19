namespace SystemMonitor.Domain.AsciiArt;

public readonly record struct AsciiCell(char Glyph, byte R, byte G, byte B, int Tier);
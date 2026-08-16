// Domain/AsciiArt/AsciiCell.cs
namespace SystemMonitor.Domain.AsciiArt;

public readonly record struct AsciiCell(char Glyph, byte R, byte G, byte B);
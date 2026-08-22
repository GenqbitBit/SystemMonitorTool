using System.Globalization;

namespace SystemMonitor.Infrastructure.Monitoring.Linux;

internal static class LinuxFileReader
{
    public static string? ReadText(string path)
    {
        try
        {
            var value = File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static string[] ReadLines(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    public static bool TryReadLong(string path, out long value)
    {
        return long.TryParse(ReadText(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryReadDouble(string path, out double value)
    {
        return double.TryParse(ReadText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    public static string[] GetDirectories(string path)
    {
        try { return Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    public static string[] GetFiles(string path, string pattern)
    {
        try { return Directory.Exists(path) ? Directory.GetFiles(path, pattern) : Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }
}
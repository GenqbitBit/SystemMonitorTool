using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;

internal static class MacOsCommandRunner
{
    public static string Run(string fileName, params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            if (!process.Start()) return string.Empty;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { }
                return string.Empty;
            }
            return process.ExitCode == 0 ? outputTask.GetAwaiter().GetResult() : string.Empty;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            return string.Empty;
        }
    }

    public static string ReadSysctl(string name) => Run("/usr/sbin/sysctl", "-n", name).Trim();

    public static bool TryReadDouble(string value, out double result) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
        && double.IsFinite(result);

    public static bool TryReadLong(string value, out long result) =>
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    public static Dictionary<string, long> ParseVmStat(string output)
    {
        var values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().TrimEnd('.');
            if (TryReadLong(value, out var pages)) values[key] = pages;
        }
        return values;
    }

    public static JsonDocument? ParseJson(string output)
    {
        try { return string.IsNullOrWhiteSpace(output) ? null : JsonDocument.Parse(output); }
        catch (JsonException) { return null; }
    }

    public static string? JsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        return null;
    }

    public static IEnumerable<JsonElement> Descendants(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in Descendants(property.Value)) yield return child;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in Descendants(item)) yield return child;
            }
        }
    }

    public static Dictionary<string, string> ParsePlist(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var document = XDocument.Parse(output);
            var dict = document.Descendants("dict").FirstOrDefault();
            if (dict is null) return values;
            var children = dict.Elements().ToList();
            for (var index = 0; index + 1 < children.Count; index += 2)
            {
                if (children[index].Name.LocalName != "key") continue;
                var value = children[index + 1];
                values[children[index].Value] = value.Name.LocalName switch
                {
                    "true" => "true",
                    "false" => "false",
                    _ => value.Value
                };
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException)
        {
        }
        return values;
    }
}

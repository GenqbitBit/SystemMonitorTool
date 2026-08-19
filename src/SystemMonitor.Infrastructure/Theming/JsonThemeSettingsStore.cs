using System;
using System.IO;
using System.Text.Json;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Infrastructure.Theming;

public class JsonThemeSettingsStore : IThemeSettingsStore
{
    private readonly string _filePath;

    public JsonThemeSettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemMonitor");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "theme-settings.json");
    }

    public ThemeSettings? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ThemeSettings>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(ThemeSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
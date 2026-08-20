using System;
using System.IO;
using System.Text.Json;
using SystemMonitor.Application.Common;
using SystemMonitor.Application.Interfaces;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Infrastructure.Persistence;

public sealed class JsonSettingsService : ISettingsService, IDisposable
{
    private readonly string _filePath;
    private readonly object _gate = new();
    private readonly CoalescingDispatcher<AppSettings> _coalescer;

    private AppSettings _current;

    public AppSettings Current
    {
        get { lock (_gate) return _current; }
    }

    // Fires on a timer thread, NOT the UI thread — Infrastructure has no
    // knowledge of Avalonia. Subscribers on the Presentation side must
    // marshal to Dispatcher.UIThread themselves if they touch bound state.
    public event Action<AppSettings>? SettingsApplied;

    public JsonSettingsService(string filePath)
    {
        _filePath = filePath;
        _current = Load();
        _coalescer = new CoalescingDispatcher<AppSettings>(
            ApplyAndPersist,
            quietWindow: TimeSpan.FromMilliseconds(150),
            maxWait: TimeSpan.FromMilliseconds(500));
    }

    public void Update(Func<AppSettings, AppSettings> mutate)
    {
        AppSettings next;
        lock (_gate)
        {
            next = mutate(_current);
            _current = next;
        }

        _coalescer.Post(next);
    }

    public void ResetToDefaults()
    {
        // Preserve the current GraphStyle selection while resetting all other settings
        var currentGraphStyle = _current.GraphStyle;
        Update(current =>
        {
            var defaults = AppSettings.Defaults;
            return defaults with { GraphStyle = currentGraphStyle };
        });
    }

    private void ApplyAndPersist(AppSettings settings)
    {
        Save(settings);
        SettingsApplied?.Invoke(settings);
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // fall through to defaults on any read/parse failure
        }

        return AppSettings.Defaults;
    }

    private void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // best-effort persistence; in-memory Current is still correct
        }
    }

    public void Dispose() => _coalescer.Dispose();
}
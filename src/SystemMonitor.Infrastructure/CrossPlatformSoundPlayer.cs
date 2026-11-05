using System.Collections.Concurrent;
using System.Diagnostics;
using SystemMonitor.Application.Interfaces;

namespace SystemMonitor.Infrastructure.Audio;

public sealed class CrossPlatformSoundPlayer : IAlertSoundPlayer
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTime> _lastPlayed = new();

    public bool IsEnabled { get; set; } = true;

    public void Play(string metric, AlertLevel level)
    {
        if (!IsEnabled) return;

        // Cooldown per metric at per level
        var key = $"{metric}:{level}";
        var now = DateTime.UtcNow;
        if (_lastPlayed.TryGetValue(key, out var last) && now - last < Cooldown)
            return;
        _lastPlayed[key] = now;

        var file = level == AlertLevel.Critical ? "critical.wav" : "warning.wav";
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", file);
        if (!File.Exists(path)) return;

        Task.Run(() => PlayFile(path));
    }

    private static void PlayFile(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var safePath = path.Replace("'", "''");
                RunProcess("powershell",
                    "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden",
                    "-Command", $"(New-Object Media.SoundPlayer '{safePath}').PlaySync()");
            }
            else if (OperatingSystem.IsMacOS())
            {
                RunProcess("afplay", path);
            }
            else if (OperatingSystem.IsLinux())
            {
                if (!RunProcess("paplay", path))
                    RunProcess("aplay", path);
            }
        }
        catch
        {
            // Huwag hayaang mag-crash ang app dahil sa sound
        }
    }

    private static bool RunProcess(string exe, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
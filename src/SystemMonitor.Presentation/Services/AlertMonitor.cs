using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Presentation.Services;

public sealed class AlertMonitor
{
    public double RamThresholdPercent { get; set; } = 90;
    public double TempThresholdC { get; set; } = 85;
    public double ResetMargin { get; set; } = 5;
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(2);
    public bool UseSystemNotifications { get; set; } = true;

    private readonly WindowNotificationManager _notifier;
    private readonly Dictionary<string, DateTime> _lastAlert = new();
    private readonly HashSet<string> _active = new();

    public AlertMonitor(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _notifier = new WindowNotificationManager(window)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3,
        };
    }

    public void Check(IReadOnlyList<MetricReading> snapshot)
    {
        var ramPercent = snapshot.FirstOrDefault(m => m.Id == "memory.usage")?.Value ?? 0;
        Evaluate("ram", ramPercent, RamThresholdPercent,
            "High RAM usage",
            $"RAM is at {ramPercent:F0}%. Close some apps.");

        var hottestByComponent = snapshot
            .Where(m => m.Kind == MetricKind.Temperature && m.IsAvailable && m.Value > 0)
            .GroupBy(m => m.Category)
            .Select(group => group.MaxBy(m => m.Value)!)
            .ToList();

        foreach (var hottest in hottestByComponent)
        {
            var component = hottest.Category;
            Evaluate(
                $"temperature:{component}",
                hottest.Value,
                TempThresholdC,
                $"{component} overheating",
                $"{hottest.Label} is {hottest.Value:F0}°C.");
        }
    }

    public void Check(double ramPercent, double? cpuTempC)
    {
        Evaluate("ram", ramPercent, RamThresholdPercent,
            "High RAM usage",
            $"RAM is at {ramPercent:F0}%. Close some apps.");

        if (cpuTempC.HasValue)
        {
            Evaluate("cpu", cpuTempC.Value, TempThresholdC,
                "CPU overheating",
                $"CPU temperature is {cpuTempC.Value:F0}°C.");
        }
    }

    private void Evaluate(string key, double value, double threshold, string title, string message)
    {
        if (value >= threshold)
        {
            var cooledDown = !_lastAlert.TryGetValue(key, out var last) || DateTime.Now - last >= Cooldown;
            if (!_active.Contains(key) || cooledDown)
            {
                _active.Add(key);
                _lastAlert[key] = DateTime.Now;
                Notify(title, message);
            }
        }
        else if (value < threshold - ResetMargin)
        {
            _active.Remove(key);
        }
    }

    private void Notify(string title, string message)
    {
        Dispatcher.UIThread.Post(() =>
            _notifier.Show(new Notification(title, message, NotificationType.Warning)));

        if (UseSystemNotifications)
            ShowSystemNotification(title, message);
    }

    private static void ShowSystemNotification(string title, string message)
    {
        try
        {
            var psi = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };

            if (OperatingSystem.IsLinux())
            {
                psi.FileName = "notify-send";
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(message);
            }
            else if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "osascript";
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add($"display notification \"{message}\" with title \"{title}\"");
            }
            else if (OperatingSystem.IsWindows())
            {
                psi.FileName = "powershell";
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(
                    "Add-Type -AssemblyName System.Windows.Forms;" +
                    "$n = New-Object System.Windows.Forms.NotifyIcon;" +
                    "$n.Icon = [System.Drawing.SystemIcons]::Warning;" +
                    "$n.Visible = $true;" +
                    $"$n.ShowBalloonTip(5000, '{title}', '{message}', 'Warning');" +
                    "Start-Sleep -Seconds 6; $n.Dispose()");
            }
            else
            {
                return;
            }

            Process.Start(psi);
        }
        catch
        {
            // The app toast still works; the system notification is optional.
        }
    }

}

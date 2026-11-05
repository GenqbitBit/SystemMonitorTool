namespace SystemMonitor.Application.Interfaces;

public enum AlertLevel
{
    Warning,
    Critical
}

public interface IAlertSoundPlayer
{
    /// <summary> on/off sa Settings.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Mag-play ng sound.</summary>
    void Play(string metric, AlertLevel level);
}
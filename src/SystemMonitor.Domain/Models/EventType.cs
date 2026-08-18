namespace SystemMonitor.Domain.Models;

public static class EventType
{
    public const string ThresholdCrossed = "threshold_crossed";
    public const string AlertFired = "alert_fired";
    public const string AlertAcknowledged = "alert_acknowledged";
    public const string ConfigChanged = "config_changed";
    public const string Error = "error";
    public const string SessionStart = "session_start";
}
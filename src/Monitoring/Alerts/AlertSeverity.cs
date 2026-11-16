namespace MarstekOverlay.Monitoring.Alerts;

public enum AlertSeverity
{
    Info,
    Warning,

    /// <summary>Worth an alert even when it was already so by the time the app began to watch.</summary>
    Critical,
}

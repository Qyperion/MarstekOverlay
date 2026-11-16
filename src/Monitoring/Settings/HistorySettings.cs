namespace MarstekOverlay.Monitoring.Settings;

public class HistorySettings
{
    /// <summary>Samples older than this are deleted. One is taken on every poll, 2880 a day at the default interval.</summary>
    public int RetentionDays { get; set; } = 7;
}

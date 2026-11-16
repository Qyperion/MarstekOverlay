using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.Monitoring.Settings;

public static class HistorySettingsValidator
{
    public const int MaxRetentionDays = 3650;

    // Anything shorter would not cover what is kept in memory for the charts.
    public static readonly int MinRetentionDays = (int)Math.Ceiling(SampleHistory.KeptInMemory.TotalDays);

    public static bool IsValid(HistorySettings settings)
    {
        return settings is not null
            && settings.RetentionDays >= MinRetentionDays
            && settings.RetentionDays <= MaxRetentionDays;
    }
}

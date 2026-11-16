namespace MarstekOverlay.Monitoring.Settings;

public static class PollingSettingsValidator
{
    // The windows of the forecast, of the outage rule and of the silence alert are minutes long and need
    // several polls inside them.
    public const int MaxIntervalSeconds = 120;
    public const int MaxSlowIntervalSeconds = 3600;

    public static bool IsValid(PollingSettings settings)
    {
        return settings is not null
            && settings.IntervalSeconds is > 0 and <= MaxIntervalSeconds
            && settings.SlowIntervalSeconds >= settings.IntervalSeconds
            && settings.SlowIntervalSeconds <= MaxSlowIntervalSeconds
            && settings.DisabledMethods is not null
            && settings.DisabledMethods.All(method => !string.IsNullOrWhiteSpace(method));
    }
}

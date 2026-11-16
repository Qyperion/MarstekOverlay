namespace MarstekOverlay.LocalApi.Settings;

public static class MarstekApiSettingsValidator
{
    public static bool IsValid(MarstekApiSettings settings)
    {
        return settings is not null
            && settings.RequestTimeoutMs > 0
            && settings.RetryCount >= 0
            && settings.MinDelayBetweenRequestsMs >= 0
            && settings.DiscoveryDurationMs > 0;
    }
}

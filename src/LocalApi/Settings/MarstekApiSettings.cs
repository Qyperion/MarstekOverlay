namespace MarstekOverlay.LocalApi.Settings;

public class MarstekApiSettings
{
    public int RequestTimeoutMs { get; set; } = 3000;

    /// <summary>Answers get lost on Wi-Fi often enough that one silent request says little about the station.</summary>
    public int RetryCount { get; set; } = 1;

    /// <summary>A Venus E 3.0 on firmware 150 silently drops a request sent within about 0.5 s of the previous
    /// one and answers every request sent 0.75 s apart or more.</summary>
    public int MinDelayBetweenRequestsMs { get; set; } = 1000;

    public int DiscoveryDurationMs { get; set; } = 3000;
}

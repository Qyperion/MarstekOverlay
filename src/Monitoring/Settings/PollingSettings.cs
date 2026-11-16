namespace MarstekOverlay.Monitoring.Settings;

public class PollingSettings
{
    /// <summary>How often the power flows, the operating mode and the meter are read.</summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>How often the slowly changing data is read: identity, battery details, Wi-Fi and Bluetooth.</summary>
    public int SlowIntervalSeconds { get; set; } = 300;

    /// <summary>The pause the transport keeps between requests, copied from
    /// <c>MarstekApi:MinDelayBetweenRequestsMs</c> at startup: it sets how long a poll takes.</summary>
    public int RequestSpacingMs { get; set; } = 1000;

    /// <summary>Wire names of methods that are never sent, such as "Bat.GetStatus" on firmware that restarts on it.</summary>
    public List<string> DisabledMethods { get; set; } = [];
}

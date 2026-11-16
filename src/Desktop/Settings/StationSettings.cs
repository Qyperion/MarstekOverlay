using MarstekOverlay.LocalApi;

namespace MarstekOverlay.Desktop.Settings;

public class StationSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = StationEndpoint.DefaultPort;

    /// <returns>Null until a valid address is saved.</returns>
    public StationEndpoint? ToEndpoint() => StationEndpoint.TryCreate(Host, Port, out var endpoint) ? endpoint : null;
}

namespace MarstekOverlay.LocalApi.Models;

public sealed record WifiStatus
{
    public string? Mac { get; init; }
    public string? Ssid { get; init; }
    public double? SignalStrengthDbm { get; init; }
    public string? Ip { get; init; }
    public string? Gateway { get; init; }
    public string? SubnetMask { get; init; }
    public string? Dns { get; init; }
}

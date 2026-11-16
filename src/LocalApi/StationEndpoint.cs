using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace MarstekOverlay.LocalApi;

public sealed record StationEndpoint(IPAddress Address, int Port)
{
    /// <summary>The factory default; the Marstek app lets the owner move it, preferably into 49152–65535.</summary>
    public const int DefaultPort = 30000;

    public static bool IsValidPort(int port) => port is > IPEndPoint.MinPort and <= IPEndPoint.MaxPort;

    public static bool TryCreate(string? host, int port, [NotNullWhen(true)] out StationEndpoint? endpoint)
    {
        endpoint = TryParseAddress(host, out var address) && IsValidPort(port) ? new StationEndpoint(address, port) : null;
        return endpoint is not null;
    }

    /// <summary>Four decimal numbers only. <see cref="IPAddress.TryParse(string?, out IPAddress?)"/> also takes the
    /// short forms of <c>inet_aton</c>, which turn an unfinished address into another one — "192.168.1" into
    /// 192.168.0.1 — and a number with a leading zero into an octal one — "050" into 40.</summary>
    public static bool TryParseAddress(string? text, [NotNullWhen(true)] out IPAddress? address)
    {
        var trimmed = text?.Trim();

        address = IPAddress.TryParse(trimmed, out var parsed)
            && parsed.AddressFamily == AddressFamily.InterNetwork
            && parsed.ToString() == trimmed
                ? parsed
                : null;

        return address is not null;
    }

    public IPEndPoint ToIPEndPoint() => new(Address, Port);

    public override string ToString() => $"{Address}:{Port}";
}

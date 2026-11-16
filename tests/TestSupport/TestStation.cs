using MarstekOverlay.LocalApi;

using System.Net;

namespace MarstekOverlay.TestSupport;

/// <summary>A station on the home network, where nothing ever answers from a test.</summary>
public static class TestStation
{
    public const string Host = "192.168.1.50";

    public static readonly StationEndpoint Endpoint = new(IPAddress.Parse(Host), StationEndpoint.DefaultPort);
}

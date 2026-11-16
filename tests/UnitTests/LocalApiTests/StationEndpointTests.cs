using FluentAssertions;

using MarstekOverlay.LocalApi;

using System.Net;

using Xunit;

namespace MarstekOverlay.UnitTests.LocalApiTests;

public class StationEndpointTests
{
    [Fact]
    public void TryCreate_ValidAddressAndPort_Succeeds()
    {
        // Act
        var created = StationEndpoint.TryCreate(" 192.168.1.50 ", 30000, out var endpoint);

        // Assert
        created.Should().BeTrue();
        endpoint.Should().Be(new StationEndpoint(IPAddress.Parse("192.168.1.50"), 30000));
        (endpoint?.ToString()).Should().Be("192.168.1.50:30000");
    }

    [Theory]
    [InlineData(null, 30000)]
    [InlineData("", 30000)]
    [InlineData("station.local", 30000)]
    [InlineData("fe80::1", 30000)]
    [InlineData("192.168.1.50", 0)]
    [InlineData("192.168.1.50", 65536)]
    [InlineData("192.168.1", 30000)]
    [InlineData("1", 30000)]
    [InlineData("192.168.1.050", 30000)]
    [InlineData("0x7f.0.0.1", 30000)]
    [InlineData("192.168.1.256", 30000)]
    public void TryCreate_AnythingButIPv4WithARealPort_Fails(string? host, int port)
    {
        // Act
        var created = StationEndpoint.TryCreate(host, port, out var endpoint);

        // Assert
        created.Should().BeFalse();
        endpoint.Should().BeNull();
    }
}

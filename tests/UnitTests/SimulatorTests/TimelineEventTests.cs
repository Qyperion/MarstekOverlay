using FluentAssertions;

using MarstekOverlay.StationSimulator;

using Xunit;

namespace MarstekOverlay.UnitTests.SimulatorTests;

public class TimelineEventTests
{
    [Theory]
    [InlineData("60:outage", 60, TimelineAction.Outage, null, RpcErrors.InvalidParams)]
    [InlineData("90.5:Mains", 90.5, TimelineAction.Mains, null, RpcErrors.InvalidParams)]
    [InlineData("10:silent:ES.GetMode", 10, TimelineAction.Silent, "ES.GetMode", RpcErrors.InvalidParams)]
    [InlineData("10:refuse:ES.GetMode", 10, TimelineAction.Refuse, "ES.GetMode", RpcErrors.InvalidParams)]
    [InlineData("90:refuse:ES.GetMode:-32700", 90, TimelineAction.Refuse, "ES.GetMode", RpcErrors.ParseError)]
    [InlineData("0:offline", 0, TimelineAction.Offline, null, RpcErrors.InvalidParams)]
    public void TryParse_WellFormed_ReadsEveryPart(string text, double seconds, TimelineAction action, string? method, int code)
    {
        // Act
        var parsed = TimelineEvent.TryParse(text, out var timelineEvent);

        // Assert
        parsed.Should().BeTrue();
        timelineEvent.Should().Be(new TimelineEvent(TimeSpan.FromSeconds(seconds), action, method, code));
    }

    [Theory]
    [InlineData("60")]
    [InlineData("5:0")]
    [InlineData("5:2")]
    [InlineData("-5:outage")]
    [InlineData("Infinity:outage")]
    [InlineData("604801:outage")]
    [InlineData("60:pause")]
    [InlineData("60:outage:ES.GetMode")]
    [InlineData("60:silent")]
    [InlineData("60:silent:ES.GetMode:-32603")]
    [InlineData("60:refuse:ES.GetMode:x")]
    [InlineData("60:refuse:ES.GetMode:-32603:1")]
    public void TryParse_OutOfShape_Fails(string text)
    {
        // Act
        var parsed = TimelineEvent.TryParse(text, out _);

        // Assert
        parsed.Should().BeFalse();
    }
}

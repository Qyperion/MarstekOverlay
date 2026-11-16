using FluentAssertions;

using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Settings;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class SilenceLimitTests
{
    [Theory]
    [InlineData(5, 30, 300)]
    [InlineData(1, 30, 60)]
    [InlineData(1, 45, 90)]
    [InlineData(1, 120, 240)]
    [InlineData(3, 120, 240)]
    [InlineData(5, 120, 300)]
    public void Of_ChosenTimeAndPoll_IsTheLongerOfTheTimeAndTwoPollIntervals(double chosenMinutes, int intervalSeconds, int expectedSeconds)
    {
        // Arrange
        var polling = new PollingSettings { IntervalSeconds = intervalSeconds };

        // Act
        var limit = SilenceLimit.Of(chosenMinutes, polling);

        // Assert
        limit.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(100000, 86400)]
    public void Of_TimeFromAFileEditedByHand_StaysWithinWhatTheWindowAccepts(double chosenMinutes, int expectedSeconds)
    {
        // Act
        var limit = SilenceLimit.Of(chosenMinutes, new PollingSettings { IntervalSeconds = 5 });

        // Assert
        limit.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }
}

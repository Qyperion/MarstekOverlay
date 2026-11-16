using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Settings;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class ReadingFreshnessTests
{
    private static readonly PollingSettings Polling = new() { IntervalSeconds = 30, SlowIntervalSeconds = 300 };

    [Theory]
    [InlineData(MarstekMethods.EnergySystemGetStatus)]
    [InlineData(MarstekMethods.EnergySystemGetMode)]
    [InlineData(MarstekMethods.EnergyMeterGetStatus)]
    [InlineData(MarstekMethods.PvGetStatus)]
    public void TrustedFor_RequestOfEveryPoll_IsFourCyclesOfIntervalAndPoll(string method)
    {
        ReadingFreshness.TrustedFor(method, Polling).Should().Be(TimeSpan.FromSeconds(4 * (30 + 4)));
    }

    [Theory]
    [InlineData(MarstekMethods.GetDevice)]
    [InlineData(MarstekMethods.BatteryGetStatus)]
    [InlineData(MarstekMethods.WifiGetStatus)]
    [InlineData(MarstekMethods.BluetoothGetStatus)]
    public void TrustedFor_RequestOfTheSlowPoll_IsThreeIntervalsOfThatEachUpToAFastCycleLate(string method)
    {
        ReadingFreshness.TrustedFor(method, Polling).Should().Be(TimeSpan.FromSeconds(3 * (300 + 30 + 4)));
    }

    [Fact]
    public void TrustedFor_OtherIntervals_FollowsThem()
    {
        // Arrange
        var polling = new PollingSettings { IntervalSeconds = 5, SlowIntervalSeconds = 60, RequestSpacingMs = 500 };

        // Act & Assert
        ReadingFreshness.TrustedFor(MarstekMethods.EnergySystemGetStatus, polling).Should().Be(TimeSpan.FromSeconds(28));
        ReadingFreshness.TrustedFor(MarstekMethods.BatteryGetStatus, polling).Should().Be(TimeSpan.FromSeconds(201));
    }

    [Fact]
    public void TrustedFor_ShortInterval_OutlastsTwoPollsLostInARow()
    {
        // Arrange
        var polling = new PollingSettings { IntervalSeconds = 5, SlowIntervalSeconds = 60 };
        var cycle = TimeSpan.FromSeconds(5) + 4 * TimeSpan.FromMilliseconds(polling.RequestSpacingMs);

        // Act
        var trustedFor = ReadingFreshness.TrustedFor(MarstekMethods.EnergySystemGetStatus, polling);

        // Assert
        trustedFor.Should().BeGreaterThan(3 * cycle, "the answer after two lost ones comes three cycles later");
    }

    [Fact]
    public void TrustedFor_UsualInterval_IsNoShorterThanFourIntervals()
    {
        ReadingFreshness.TrustedFor(MarstekMethods.EnergySystemGetStatus, Polling).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void TrustedFor_MethodOutsideThePoll_IsTurnedDown()
    {
        // Act
        var act = () => ReadingFreshness.TrustedFor("ES.SetMode", Polling);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

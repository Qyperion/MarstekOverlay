using FluentAssertions;

using MarstekOverlay.Monitoring.Alerts;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class AlertSettingsTests
{
    [Theory]
    [InlineData(100, ChargeLevel.Comfortable)]
    [InlineData(51, ChargeLevel.Comfortable)]
    [InlineData(50, ChargeLevel.Low)]
    [InlineData(30, ChargeLevel.Low)]
    [InlineData(16, ChargeLevel.Low)]
    [InlineData(15, ChargeLevel.Critical)]
    [InlineData(0, ChargeLevel.Critical)]
    public void ChargeLevelAt_DefaultThresholds_IsCriticalFromTheLowestAndLowFromTheHighest(double soc, ChargeLevel expected)
    {
        new AlertSettings().ChargeLevelAt(soc).Should().Be(expected);
    }

    [Fact]
    public void ChargeLevelAt_NoThresholds_IsAlwaysComfortable()
    {
        var settings = new AlertSettings { ChargeThresholdsPercent = [] };

        settings.ChargeLevelAt(1).Should().Be(ChargeLevel.Comfortable);
    }

    [Fact]
    public void ChargeLevelAt_SingleThreshold_IsCriticalBelowIt()
    {
        var settings = new AlertSettings { ChargeThresholdsPercent = [25] };

        settings.ChargeLevelAt(26).Should().Be(ChargeLevel.Comfortable);
        settings.ChargeLevelAt(25).Should().Be(ChargeLevel.Critical);
    }

    [Fact]
    public void UsableChargeThresholds_FileEditedByHand_AreSortedDistinctAndWithinRange()
    {
        var settings = new AlertSettings { ChargeThresholdsPercent = [30, 0, 150, 15, 30, -5, 100] };

        settings.UsableChargeThresholds().Should().Equal(15, 30, 100);
    }

    [Fact]
    public void Watch_SwitchedOffAndOnAgain_LeavesNoTrace()
    {
        // Arrange
        var settings = new AlertSettings();

        // Act
        settings.Watch(AlertKind.StaleData, isWatched: false);
        settings.Watch(AlertKind.StaleData, isWatched: false);
        var whileOff = settings.Watches(AlertKind.StaleData);
        settings.Watch(AlertKind.StaleData, isWatched: true);

        // Assert
        whileOff.Should().BeFalse();
        settings.Watches(AlertKind.StaleData).Should().BeTrue();
        settings.DisabledKinds.Should().BeEmpty();
    }
}

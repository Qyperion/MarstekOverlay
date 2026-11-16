using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class BatteryPowerReadingTests
{
    [Fact]
    public void From_ReportedBatteryPower_IsUsedAsIs()
    {
        // Arrange
        var status = new EnergySystemStatus
        {
            BatteryPower = new Power(-120),
            PvPower = new Power(900),
            OngridPower = Power.Zero,
            OffgridPower = Power.Zero,
        };

        // Act & Assert
        BatteryPowerReading.From(status).Should().Be(new BatteryPowerReading(new Power(-120), IsDerived: false));
    }

    [Theory]
    [InlineData(0, 0, 284, -284, BatteryFlow.Discharging)]
    [InlineData(600, 100, 200, 300, BatteryFlow.Charging)]
    [InlineData(0, -800, 0, 800, BatteryFlow.Charging)]
    [InlineData(300, 300, 0, 0, BatteryFlow.Idle)]
    public void From_NoReportedBatteryPower_IsWhatPvLeavesAfterBothOutputs(
        double pvWatts,
        double ongridWatts,
        double offgridWatts,
        double expectedWatts,
        BatteryFlow expectedFlow)
    {
        // Arrange
        var status = new EnergySystemStatus
        {
            PvPower = new Power(pvWatts),
            OngridPower = new Power(ongridWatts),
            OffgridPower = new Power(offgridWatts),
        };

        // Act
        var reading = BatteryPowerReading.From(status);

        // Assert
        reading.Should().Be(new BatteryPowerReading(new Power(expectedWatts), IsDerived: true));
        (reading?.Flow).Should().Be(expectedFlow);
    }

    [Fact]
    public void From_AFlowMissing_GivesNothingRatherThanAGuess()
    {
        // Arrange
        var status = new EnergySystemStatus { PvPower = Power.Zero, OngridPower = Power.Zero };

        // Act & Assert
        BatteryPowerReading.From(status).Should().BeNull();
    }
}

using FluentAssertions;

using MarstekOverlay.StationSimulator;

using Xunit;

namespace MarstekOverlay.UnitTests.SimulatorTests;

public class SimulatedStationTests
{
    private const double ChargingWatts = 2200;

    private static readonly DateTimeOffset Start = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Read_OnBatteryWithNoLoad_NeitherReadsANegativeLoadNorGainsCharge()
    {
        // Arrange
        var station = new SimulatedStation(new SimulatorOptions { Scenario = Scenario.Battery, SocPercent = 50, LoadWatts = 0 }, Start);

        // Act
        var readings = Enumerable.Range(1, 20).Select(minute => station.Read(Start.AddMinutes(minute))).ToList();

        // Assert
        readings.Should().OnlyContain(reading => reading.LoadWatts >= 0 && reading.SocPercent <= 50);
    }

    [Fact]
    public void Read_OnBattery_GivesUpTheLoadOverTheDischargeEfficiency()
    {
        // Arrange
        var station = new SimulatedStation(new SimulatorOptions { Scenario = Scenario.Battery, SocPercent = 80, LoadWatts = 250 }, Start);

        // Act
        var reading = station.Read(Start.AddMinutes(10));

        // Assert
        var drainedPercent = 250 / 0.92 / 6 / SimulatedStation.CapacityWattHours * 100;
        reading.SocPercent.Should().BeApproximately(80 - drainedPercent, 0.01);
        reading.OngridWatts.Should().Be(0);
        reading.IsMainsPresent.Should().BeFalse();
    }

    [Fact]
    public void Read_Charging_ShowsTheChargeAndTheLoadOnTheGridPort()
    {
        // Arrange
        var station = new SimulatedStation(new SimulatorOptions { Scenario = Scenario.Charging, SocPercent = 60 }, Start);

        // Act
        var reading = station.Read(Start);

        // Assert
        reading.ChargingWatts.Should().Be(ChargingWatts);
        reading.OngridWatts.Should().Be(-(ChargingWatts + reading.LoadWatts));
    }

    [Fact]
    public void Read_TopUp_ChargesWithTheSamePowerAsAFullCharge()
    {
        // Arrange
        var options = new SimulatorOptions { Scenario = Scenario.TopUp, TopUpEvery = TimeSpan.FromMinutes(1) };
        var station = new SimulatedStation(options, Start);

        // Act
        var held = station.Read(Start.AddSeconds(59));
        var toppingUp = station.Read(Start.AddSeconds(61));

        // Assert
        held.SocPercent.Should().Be(100);
        held.OngridWatts.Should().Be(0);
        toppingUp.SocPercent.Should().BeInRange(99.7, 100);
        toppingUp.ChargingWatts.Should().Be(ChargingWatts);
        toppingUp.OngridWatts.Should().Be(-(ChargingWatts + toppingUp.LoadWatts));
    }

    [Fact]
    public void Read_ChargingForLong_StopsAtFull()
    {
        // Arrange
        var station = new SimulatedStation(new SimulatorOptions { Scenario = Scenario.Charging, SocPercent = 99, Speed = 100 }, Start);

        // Act
        var reading = station.Read(Start.AddMinutes(5));

        // Assert
        reading.SocPercent.Should().Be(100);
        reading.ChargingWatts.Should().Be(0);
    }
}

using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Supply;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class PowerSourceRuleTests
{
    [Theory]
    // The rows with a load of 190 to 300 W are what Venus E 3.0 on firmware 150 reported in each state.
    [InlineData(85, 0, 257, SupplyEvidence.DrainingBattery)]
    [InlineData(99, 0, 250, SupplyEvidence.DrainingBattery)]
    [InlineData(85, 0, 21, SupplyEvidence.DrainingBattery)]
    [InlineData(83, -2482, 195, SupplyEvidence.GridAtWork)]
    [InlineData(99, -2491, 297, SupplyEvidence.GridAtWork)]
    [InlineData(60, 400, 0, SupplyEvidence.GridAtWork)]
    [InlineData(85, -11, 250, SupplyEvidence.GridAtWork)]
    [InlineData(100, 0, 259, SupplyEvidence.FullBattery)]
    [InlineData(100, 0, 0, SupplyEvidence.FullBattery)]
    [InlineData(85, 0, 0, SupplyEvidence.None)]
    [InlineData(85, 0, 20, SupplyEvidence.None)]
    public void Read_TellsFromThePowerFlowsAndTheCharge(double soc, double ongridWatts, double offgridWatts, SupplyEvidence expected)
    {
        // Arrange
        var status = Status(soc, ongridWatts, offgridWatts);

        // Act & Assert
        PowerSourceRule.Read(status).Should().Be(expected);
    }

    [Fact]
    public void Read_GridEnergyCountedSinceThePreviousAnswer_IsTheGridAtWorkEvenWithItsPortSilent()
    {
        // Arrange
        var before = Status(soc: 99, ongridWatts: 0, offgridWatts: 250) with { TotalGridInputEnergy = new Energy(18027) };
        var after = before with { BatterySocPercent = 100, TotalGridInputEnergy = new Energy(18042) };

        // Act & Assert
        PowerSourceRule.Read(after, GridEnergyCounters.Of(before)).Should().Be(SupplyEvidence.GridAtWork);
        PowerSourceRule.Read(after, GridEnergyCounters.Of(after)).Should().Be(SupplyEvidence.FullBattery);
        PowerSourceRule.Read(after).Should().Be(SupplyEvidence.FullBattery);
    }

    [Fact]
    public void Read_SolarCoversTheLoad_TellsNothing()
    {
        // Arrange
        var status = Status(soc: 85, ongridWatts: 0, offgridWatts: 250) with { PvPower = new Power(300) };

        // Act & Assert
        PowerSourceRule.Read(status).Should().Be(SupplyEvidence.None);
    }

    [Fact]
    public void Read_ReportedBatteryPower_IsTakenOverTheDerivedOne()
    {
        // Arrange
        var status = new EnergySystemStatus { BatterySocPercent = 85, OngridPower = Power.Zero, BatteryPower = new Power(-180) };

        // Act & Assert
        PowerSourceRule.Read(status).Should().Be(SupplyEvidence.DrainingBattery);
    }

    [Fact]
    public void Read_NoGridPowerOrNoChargeInTheAnswer_TellsNothing()
    {
        // Arrange
        var draining = new EnergySystemStatus { BatterySocPercent = 85, OngridPower = Power.Zero, BatteryPower = new Power(-180) };

        // Act & Assert
        PowerSourceRule.Read(draining with { OngridPower = null }).Should().Be(SupplyEvidence.None);
        PowerSourceRule.Read(draining with { BatterySocPercent = null }).Should().Be(SupplyEvidence.None);
    }

    [Theory]
    // On the mains with the grid port silent the load is passed through, so none of it is the battery's.
    [InlineData(PowerSource.Mains, 0, 259, 0)]
    [InlineData(PowerSource.Mains, -2482, 195, 2287)]
    [InlineData(PowerSource.Battery, 0, 259, -259)]
    [InlineData(PowerSource.Unknown, 0, 259, -259)]
    public void BatteryPower_KnowsThatTheGridPassedThroughShowsNowhere(
        PowerSource source,
        double ongridWatts,
        double offgridWatts,
        double expectedWatts)
    {
        // Arrange
        var status = Status(soc: 100, ongridWatts, offgridWatts);

        // Act & Assert
        PowerSourceRule.BatteryPower(status, source).Should().Be(new BatteryPowerReading(new Power(expectedWatts), IsDerived: true));
    }

    [Fact]
    public void BatteryPower_ReportedByTheStation_IsLeftAsItIs()
    {
        // Arrange
        var status = Status(soc: 100, ongridWatts: 0, offgridWatts: 259) with { BatteryPower = new Power(-12) };

        // Act & Assert
        PowerSourceRule.BatteryPower(status, PowerSource.Mains).Should().Be(new BatteryPowerReading(new Power(-12), IsDerived: false));
    }

    [Theory]
    [InlineData(99, -2491, true)]
    [InlineData(100, -2491, true)]
    [InlineData(98, -2491, false)]
    [InlineData(99, 0, false)]
    public void IsTopUp_ChargingAnAlmostFullBattery_IsTheUpkeepOfAFullOne(double soc, double ongridWatts, bool expected)
    {
        PowerSourceRule.IsTopUp(Status(soc, ongridWatts, offgridWatts: 250)).Should().Be(expected);
    }

    [Theory]
    [InlineData(PowerSource.Mains, 40, -2482, true)]
    [InlineData(PowerSource.Mains, 98, -2482, true)]
    // A top-up of a full battery is not the charging of an emptied one.
    [InlineData(PowerSource.Mains, 99, -2491, false)]
    // The grid passed through to the backup output charges nothing.
    [InlineData(PowerSource.Mains, 40, 0, false)]
    [InlineData(PowerSource.Battery, 40, 0, false)]
    [InlineData(PowerSource.Unknown, 40, -2482, false)]
    public void IsChargingFromMains_TakesTheMainsAndABatteryWithRoomToCharge(
        PowerSource source,
        double soc,
        double ongridWatts,
        bool expected)
    {
        PowerSourceRule.IsChargingFromMains(Status(soc, ongridWatts, offgridWatts: 250), source).Should().Be(expected);
    }

    [Theory]
    // One answer with a silent grid port: the load is the battery's or the grid's, and nothing tells which.
    [InlineData(PowerSource.Unknown, 0, 259, true)]
    [InlineData(PowerSource.Mains, 0, 259, false)]
    [InlineData(PowerSource.Battery, 0, 259, false)]
    // The grid at work is counted in, and without a load there is nothing to mistake.
    [InlineData(PowerSource.Unknown, -2482, 195, false)]
    [InlineData(PowerSource.Unknown, 0, 0, false)]
    public void IsBatteryPowerInDoubt_OnlyWhileNothingTellsWhoCarriesTheLoad(
        PowerSource source,
        double ongridWatts,
        double offgridWatts,
        bool expected)
    {
        PowerSourceRule.IsBatteryPowerInDoubt(Status(soc: 100, ongridWatts, offgridWatts), source).Should().Be(expected);
    }

    [Fact]
    public void IsBatteryPowerInDoubt_PowerReportedByTheStation_IsNotAGuess()
    {
        // Arrange
        var status = Status(soc: 100, ongridWatts: 0, offgridWatts: 259) with { BatteryPower = new Power(-12) };

        // Act & Assert
        PowerSourceRule.IsBatteryPowerInDoubt(status, PowerSource.Unknown).Should().BeFalse();
    }

    private static EnergySystemStatus Status(double soc, double ongridWatts, double offgridWatts) => new()
    {
        BatterySocPercent = soc,
        PvPower = Power.Zero,
        OngridPower = new Power(ongridWatts),
        OffgridPower = new Power(offgridWatts),
    };
}

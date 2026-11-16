using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class StoredEnergyEstimatorTests
{
    private const double Precision = 0.001;

    private static readonly Energy Capacity = new(5000);
    private static readonly ForecastSettings Settings = new() { DischargeEfficiencyPercent = 90, ChargeEfficiencyPercent = 96 };

    private readonly PollClock _time = new();
    private readonly StoredEnergyEstimator _estimator = new();

    [Fact]
    public void Update_NoBmsReadingYet_IsTheStateOfChargeShareOfTheCapacity()
    {
        Update(soc: 62, watts: -270).Should().Be(new Energy(3100));
    }

    [Fact]
    public void Update_BmsReading_IsTakenAsItIs()
    {
        Update(soc: 62, watts: -270, Bms(3150)).Should().Be(new Energy(3150));
    }

    [Fact]
    public void Update_Discharging_CarriesTheBmsReadingForwardByMoreThanTheOutputsDelivered()
    {
        // Arrange
        var bms = Bms(3150);
        Update(soc: 62, watts: -270, bms);

        // Act
        var afterOnePoll = UpdateAfterInterval(soc: 62, watts: -270, bms);
        var afterTwoPolls = UpdateAfterInterval(soc: 62, watts: -270, bms);

        // Assert
        // 270 W at the outputs with 90% of the energy reaching them is 300 W out of the battery, 2.5 Wh per poll.
        (afterOnePoll?.WattHours).Should().BeApproximately(3147.5, Precision);
        (afterTwoPolls?.WattHours).Should().BeApproximately(3145, Precision);
    }

    [Fact]
    public void Update_Charging_CarriesTheBmsReadingForwardByLessThanWasDrawn()
    {
        // Arrange
        var bms = Bms(3110);
        Update(soc: 62, watts: 400, bms);

        // Act
        var afterOnePoll = UpdateAfterInterval(soc: 62, watts: 400, bms);

        // Assert
        // 400 W for 30 s is 3.33 Wh drawn, of which 96% is stored.
        (afterOnePoll?.WattHours).Should().BeApproximately(3113.2, Precision);
    }

    [Fact]
    public void Update_NewBmsReading_ReplacesWhatWasCarriedForward()
    {
        // Arrange
        var first = Bms(3150);
        Update(soc: 62, watts: -270, first);
        UpdateAfterInterval(soc: 62, watts: -270, first);
        _time.NextPoll();

        // Act
        var estimate = Update(soc: 62, watts: -270, Bms(3140));

        // Assert
        estimate.Should().Be(new Energy(3140));
    }

    [Fact]
    public void Update_BmsReadingThatCameAfterTheStatus_IsCarriedFromTheMomentItCame()
    {
        // Arrange
        Update(soc: 62, watts: -270);
        _time.Advance(TimeSpan.FromSeconds(4));
        var bms = Bms(3150);
        _time.Advance(TimeSpan.FromSeconds(26));

        // Act
        var estimate = Update(soc: 62, watts: -270, bms);

        // Assert
        (estimate?.WattHours).Should().BeApproximately(3150 - 300.0 * 26 / 3600, Precision);
    }

    [Fact]
    public void Update_BmsReadingNewerThanTheStatus_IsTakenAsItIsAndCarriedFromItsOwnMoment()
    {
        // Arrange
        var status = new EnergySystemStatus { BatterySocPercent = 62, BatteryCapacity = Capacity, BatteryPower = new Power(-270) };
        var slowPoll = Snapshot(status, bms: null);
        Update(slowPoll);
        _time.Advance(TimeSpan.FromSeconds(4));
        var bms = Bms(3150);

        // Act
        var sameStatus = Update(slowPoll with { Battery = bms });
        _time.Advance(TimeSpan.FromSeconds(26));
        var nextPoll = Update(soc: 62, watts: -270, bms);

        // Assert
        sameStatus.Should().Be(new Energy(3150));
        (nextPoll?.WattHours).Should().BeApproximately(3150 - 300.0 * 26 / 3600, Precision);
    }

    [Theory]
    [InlineData(60, 3050)]
    [InlineData(65, 3200)]
    public void Update_CarriedValueOutsideWhatTheStateOfChargeAllows_IsKeptWithinAPercentOfIt(double soc, double expectedWattHours)
    {
        // Arrange
        var bms = Bms(3150);
        Update(soc: 62, watts: -30, bms);

        // Act
        var estimate = UpdateAfterInterval(soc, watts: -30, bms);

        // Assert
        estimate.Should().Be(new Energy(expectedWattHours));
    }

    [Fact]
    public void Update_BmsReadingFarFromTheStateOfCharge_IsKeptWithinAPercentOfIt()
    {
        Update(soc: 62, watts: -270, Bms(1000)).Should().Be(new Energy(3050));
    }

    [Fact]
    public void Update_BmsAnswerWithoutTheRemainingCapacity_ChangesNothing()
    {
        // Arrange
        var withoutCapacity = new Reading<BatteryStatus>(new BatteryStatus { TemperatureCelsius = 25 }, _time.Now);

        // Act & Assert
        Update(soc: 62, watts: -270, withoutCapacity).Should().Be(new Energy(3100));
    }

    [Fact]
    public void Update_NoBatteryPower_KeepsTheCarriedValue()
    {
        // Arrange
        var bms = Bms(3150);
        Update(soc: 62, watts: -270, bms);
        _time.NextPoll();
        var withoutPower = new EnergySystemStatus { BatterySocPercent = 62, BatteryCapacity = Capacity };

        // Act
        var estimate = Update(Snapshot(withoutPower, bms));

        // Assert
        estimate.Should().Be(new Energy(3150));
    }

    [Fact]
    public void Update_StateOfChargeOrCapacityMissing_GivesNothing()
    {
        // Arrange
        var complete = new EnergySystemStatus { BatterySocPercent = 62, BatteryCapacity = Capacity, BatteryPower = new Power(-270) };
        var bms = Bms(3150);

        // Act & Assert
        Update(StationSnapshot.Empty).Should().BeNull();
        Update(Snapshot(complete with { BatterySocPercent = null }, bms)).Should().BeNull();
        Update(Snapshot(complete with { BatterySocPercent = 130 }, bms)).Should().BeNull();
        Update(Snapshot(complete with { BatteryCapacity = null }, bms)).Should().BeNull();
        Update(Snapshot(complete with { BatteryCapacity = new Energy(0) }, bms)).Should().BeNull();
    }

    private Energy? Update(double soc, double watts, Reading<BatteryStatus>? bms = null) =>
        Update(Snapshot(new EnergySystemStatus { BatterySocPercent = soc, BatteryCapacity = Capacity, BatteryPower = new Power(watts) }, bms));

    private Energy? Update(StationSnapshot snapshot) => _estimator.Update(snapshot, BatteryPower(snapshot), Settings);

    private static BatteryPowerReading? BatteryPower(StationSnapshot snapshot) =>
        snapshot.EnergySystem is { } reading ? PowerSourceRule.BatteryPower(reading.Value, PowerSource.Unknown) : null;

    private Energy? UpdateAfterInterval(double soc, double watts, Reading<BatteryStatus>? bms)
    {
        _time.NextPoll();
        return Update(soc, watts, bms);
    }

    private StationSnapshot Snapshot(EnergySystemStatus status, Reading<BatteryStatus>? bms) => new()
    {
        EnergySystem = new Reading<EnergySystemStatus>(status, _time.Now),
        Battery = bms,
    };

    private Reading<BatteryStatus> Bms(double wattHours) =>
        new(new BatteryStatus { RemainingCapacity = new Energy(wattHours) }, _time.Now);
}

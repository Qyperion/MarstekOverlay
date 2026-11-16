using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class RuntimeForecasterTests
{
    private static readonly TimeSpan Precision = TimeSpan.FromSeconds(1);
    private static readonly Energy Capacity = new(5000);
    private static readonly ForecastSettings Settings = new()
    {
        ReserveSocPercent = 12,
        DischargeEfficiencyPercent = 90,
        ChargeEfficiencyPercent = 96,
    };

    private readonly PollClock _time = new();
    private readonly RuntimeForecaster _forecaster = new();

    [Fact]
    public void Update_SteadyDischarge_EstimatesWhenTheReserveIsReachedOnceEnoughSamplesAreIn()
    {
        // Act
        var first = Update(soc: 62, watts: -250);
        var second = UpdateAfterInterval(soc: 62, watts: -250);
        var third = UpdateAfterInterval(soc: 62, watts: -250);

        // Assert
        first.Status.Should().Be(ForecastStatus.Collecting);
        second.Status.Should().Be(ForecastStatus.Collecting);
        third.Status.Should().Be(ForecastStatus.TimeToReserve);
        // 5000 Wh × (62 − 12)% × 90% = 2250 Wh at 250 W.
        third.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(9), Precision);
    }

    [Fact]
    public void Update_VaryingLoad_UsesTheAverageOverTheWindow()
    {
        // Act
        Update(soc: 62, watts: -100);
        UpdateAfterInterval(soc: 62, watts: -200);
        var forecast = UpdateAfterInterval(soc: 62, watts: -300);

        // Assert
        forecast.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(2250.0 / 200), Precision);
    }

    [Fact]
    public void Update_SamplesOlderThanTheWindow_NoLongerCount()
    {
        // Arrange
        Update(soc: 62, watts: -1000);
        _time.Advance(RuntimeForecaster.AveragingWindow - PollClock.Interval);
        Update(soc: 62, watts: -250);
        UpdateAfterInterval(soc: 62, watts: -250);

        // Act
        var forecast = UpdateAfterInterval(soc: 62, watts: -250);

        // Assert
        forecast.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(9), Precision);
    }

    [Fact]
    public void Update_SteadyCharge_EstimatesWhenTheBatteryIsFull()
    {
        // Act
        var first = Update(soc: 64, watts: 400);
        UpdateAfterInterval(soc: 64, watts: 400);
        var third = UpdateAfterInterval(soc: 64, watts: 400);

        // Assert
        first.Status.Should().Be(ForecastStatus.Collecting);
        third.Status.Should().Be(ForecastStatus.TimeToFull);
        // 5000 Wh × (100 − 64)% = 1800 Wh to store, 1875 Wh drawn with 96% of it reaching the battery, at 400 W.
        third.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(1875.0 / 400), Precision);
    }

    [Fact]
    public void Update_ChargingBelowTheReserve_StillEstimatesTheTimeToFull()
    {
        // Act
        Update(soc: 10, watts: 500);
        UpdateAfterInterval(soc: 10, watts: 500);
        var forecast = UpdateAfterInterval(soc: 10, watts: 500);

        // Assert
        forecast.Status.Should().Be(ForecastStatus.TimeToFull);
        // 4500 Wh to store, 4687.5 Wh drawn at 500 W.
        forecast.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(4687.5 / 500), Precision);
    }

    [Fact]
    public void Update_FullBatteryStillTakingCharge_IsFullNow()
    {
        // Act
        Update(soc: 100, watts: 30);
        UpdateAfterInterval(soc: 100, watts: 30);
        var forecast = UpdateAfterInterval(soc: 100, watts: 30);

        // Assert
        forecast.Status.Should().Be(ForecastStatus.TimeToFull);
        forecast.EndsAt.Should().Be(_time.Now);
    }

    [Theory]
    [InlineData(62, -5, ForecastStatus.LoadTooLow)]
    [InlineData(62, 0, ForecastStatus.LoadTooLow)]
    [InlineData(62, 8, ForecastStatus.LoadTooLow)]
    [InlineData(12, -250, ForecastStatus.ReserveReached)]
    [InlineData(5, -250, ForecastStatus.ReserveReached)]
    [InlineData(5, 0, ForecastStatus.ReserveReached)]
    public void Update_NothingToForecast_SaysWhy(double soc, double watts, ForecastStatus expected)
    {
        // Act
        var forecast = Update(soc, watts);

        // Assert
        forecast.Status.Should().Be(expected);
        forecast.EndsAt.Should().BeNull();
    }

    [Fact]
    public void Update_OnMainsWithAFullBattery_EstimatesHowLongTheBatteryWouldCarryTheLoad()
    {
        // Act
        var first = UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);
        _time.NextPoll();
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);
        _time.NextPoll();
        var third = UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);

        // Assert
        first.Status.Should().Be(ForecastStatus.Collecting);
        third.Status.Should().Be(ForecastStatus.Standby);
        third.EndsAt.Should().BeNull();
        // 5000 Wh × (100 − 12)% × 90% = 3960 Wh at 250 W.
        third.StandbyRuntime.Should().BeCloseTo(TimeSpan.FromHours(15.84), Precision);
    }

    [Fact]
    public void Update_TopUpOfAFullBattery_DoesNotInterruptTheStandbyEstimate()
    {
        // Arrange
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);
        _time.NextPoll();
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);
        _time.NextPoll();

        // Act
        var toppingUp = UpdateOnMains(soc: 99, gridWatts: -2500, loadWatts: 250);

        // Assert
        toppingUp.Status.Should().Be(ForecastStatus.Standby);
        // 5000 Wh × (99 − 12)% × 90% = 3915 Wh at 250 W.
        toppingUp.StandbyRuntime.Should().BeCloseTo(TimeSpan.FromHours(15.66), Precision);
    }

    [Fact]
    public void Update_OnMainsChargingAnEmptiedBattery_EstimatesWhenItIsFull()
    {
        // Act
        UpdateOnMains(soc: 64, gridWatts: -650, loadWatts: 250);
        _time.NextPoll();
        UpdateOnMains(soc: 64, gridWatts: -650, loadWatts: 250);
        _time.NextPoll();
        var forecast = UpdateOnMains(soc: 64, gridWatts: -650, loadWatts: 250);

        // Assert
        forecast.Status.Should().Be(ForecastStatus.TimeToFull);
        // 400 W of the 650 W drawn go to the battery: 1800 Wh to store, 1875 Wh with the losses.
        forecast.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(1875.0 / 400), Precision);
    }

    [Fact]
    public void Update_OnMainsWithNothingOnTheBackupOutput_HasNothingToForecast()
    {
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 15).Status.Should().Be(ForecastStatus.LoadTooLow);
    }

    [Fact]
    public void Update_OutageAfterStandby_StartsTheAveragingOver()
    {
        // Arrange
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);
        _time.NextPoll();
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);
        _time.NextPoll();
        UpdateOnMains(soc: 100, gridWatts: 0, loadWatts: 250);

        // Act
        var onBattery = UpdateAfterInterval(soc: 99, watts: -250);

        // Assert
        onBattery.Status.Should().Be(ForecastStatus.Collecting);
    }

    [Fact]
    public void Update_MissingReading_IsInsufficientData()
    {
        // Arrange
        var complete = new EnergySystemStatus { BatterySocPercent = 62, BatteryCapacity = Capacity, BatteryPower = new Power(-250) };

        // Act & Assert
        Update(status: null).Status.Should().Be(ForecastStatus.InsufficientData);
        Update(complete with { BatterySocPercent = null }).Status.Should().Be(ForecastStatus.InsufficientData);
        Update(complete with { BatteryCapacity = null }).Status.Should().Be(ForecastStatus.InsufficientData);
        Update(complete with { BatteryCapacity = new Energy(0) }).Status.Should().Be(ForecastStatus.InsufficientData);
        Update(complete with { BatteryPower = null }).Status.Should().Be(ForecastStatus.InsufficientData);
    }

    [Fact]
    public void Update_ChargingInBetween_StartsTheAveragingOver()
    {
        // Arrange
        Update(soc: 62, watts: -250);
        UpdateAfterInterval(soc: 62, watts: -250);
        UpdateAfterInterval(soc: 62, watts: 400);

        // Act
        var afterCharging = UpdateAfterInterval(soc: 62, watts: -250);

        // Assert
        afterCharging.Status.Should().Be(ForecastStatus.Collecting);
    }

    [Fact]
    public void Update_SameReadingAgain_RepeatsTheForecastWithoutCountingAsASample()
    {
        // Arrange
        var failedPoll = Snapshot(soc: 62, watts: -100);
        Update(failedPoll);
        var repeated = Update(failedPoll);
        UpdateAfterInterval(soc: 62, watts: -200);

        // Act
        var forecast = UpdateAfterInterval(soc: 62, watts: -300);

        // Assert
        repeated.Status.Should().Be(ForecastStatus.Collecting);
        forecast.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(2250.0 / 200), Precision);
    }

    [Fact]
    public void Update_SettingsChangedOnTheSameReading_EstimatesAnewWithoutCountingAnotherSample()
    {
        // Arrange
        var latest = Snapshot(soc: 62, watts: -100);
        Update(latest);
        UpdateAfterInterval(soc: 62, watts: -200);
        _time.NextPoll();
        latest = Snapshot(soc: 62, watts: -300);
        var before = Update(latest);
        var higherReserve = new ForecastSettings
        {
            ReserveSocPercent = 22,
            DischargeEfficiencyPercent = Settings.DischargeEfficiencyPercent,
            ChargeEfficiencyPercent = Settings.ChargeEfficiencyPercent,
        };

        // Act
        var after = _forecaster.Update(latest, PowerSource.Unknown, higherReserve);
        var again = _forecaster.Update(latest, PowerSource.Unknown, higherReserve);

        // Assert
        before.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(2250.0 / 200), Precision);
        // 5000 Wh × (62 − 22)% × 90% = 1800 Wh, still at the 200 W of the same three samples.
        after.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours(1800.0 / 200), Precision);
        after.StoredEnergy.Should().Be(before.StoredEnergy);
        again.Should().Be(after);
    }

    [Fact]
    public void Update_ReserveRaisedAboveTheCharge_TellsAtOnce()
    {
        // Arrange
        var latest = Snapshot(soc: 62, watts: -250);
        Update(latest);
        var higherReserve = new ForecastSettings { ReserveSocPercent = 70 };

        // Act
        var forecast = _forecaster.Update(latest, PowerSource.Unknown, higherReserve);

        // Assert
        forecast.Status.Should().Be(ForecastStatus.ReserveReached);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-250)]
    [InlineData(double.NaN)]
    public void Remaining_LoadThatGivesNothingToDivideBy_IsNothing(double watts)
    {
        RuntimeForecaster.Remaining(new Energy(2250), new Power(watts)).Should().BeNull();
    }

    [Fact]
    public void Remaining_TargetAlreadyBehind_IsNone()
    {
        RuntimeForecaster.Remaining(new Energy(-30), new Power(250)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Update_BmsReading_IsWhereTheEstimateStartsFrom()
    {
        // Arrange
        var bms = new Reading<BatteryStatus>(new BatteryStatus { RemainingCapacity = new Energy(3150) }, _time.Now);

        // Act
        Update(Snapshot(soc: 62, watts: -270) with { Battery = bms });
        _time.NextPoll();
        Update(Snapshot(soc: 62, watts: -270) with { Battery = bms });
        _time.NextPoll();
        var forecast = Update(Snapshot(soc: 62, watts: -270) with { Battery = bms });

        // Assert
        // 270 W at the outputs is 300 W out of the battery: 5 Wh over the minute since the BMS answered.
        (forecast.StoredEnergy?.WattHours).Should().BeApproximately(3145, 0.001);
        forecast.EndsAt.Should().BeCloseTo(_time.Now + TimeSpan.FromHours((3145 - 600) * 0.9 / 270), Precision);
    }

    [Fact]
    public void Update_BmsAnswersAfterTheStatusOfTheSamePoll_IsTakenInWithoutWaitingForTheNextPoll()
    {
        // Arrange
        var slowPoll = Snapshot(soc: 62, watts: -270);
        Update(slowPoll);
        _time.Advance(TimeSpan.FromSeconds(4));
        var bms = new Reading<BatteryStatus>(new BatteryStatus { RemainingCapacity = new Energy(3150) }, _time.Now);

        // Act
        var forecast = Update(slowPoll with { Battery = bms });

        // Assert
        forecast.StoredEnergy.Should().Be(new Energy(3150));
    }

    [Fact]
    public void Update_NoBmsReading_CountsTheStateOfChargeShareOfTheCapacityAsStored()
    {
        Update(soc: 62, watts: -250).StoredEnergy.Should().Be(new Energy(3100));
    }

    private RuntimeForecast Update(double soc, double watts) => Update(Snapshot(soc, watts));

    private RuntimeForecast Update(EnergySystemStatus? status) =>
        Update(status is null
            ? StationSnapshot.Empty
            : new StationSnapshot { EnergySystem = new Reading<EnergySystemStatus>(status, _time.Now) });

    private RuntimeForecast Update(StationSnapshot snapshot) => _forecaster.Update(snapshot, PowerSource.Unknown, Settings);

    private RuntimeForecast UpdateOnMains(double soc, double gridWatts, double loadWatts) =>
        _forecaster.Update(
            new StationSnapshot
            {
                EnergySystem = new Reading<EnergySystemStatus>(
                    new EnergySystemStatus
                    {
                        BatterySocPercent = soc,
                        BatteryCapacity = Capacity,
                        PvPower = Power.Zero,
                        OngridPower = new Power(gridWatts),
                        OffgridPower = new Power(loadWatts),
                    },
                    _time.Now),
            },
            PowerSource.Mains,
            Settings);

    private RuntimeForecast UpdateAfterInterval(double soc, double watts)
    {
        _time.NextPoll();
        return Update(soc, watts);
    }

    private StationSnapshot Snapshot(double soc, double watts) => new()
    {
        EnergySystem = new Reading<EnergySystemStatus>(
            new EnergySystemStatus { BatterySocPercent = soc, BatteryCapacity = Capacity, BatteryPower = new Power(watts) },
            _time.Now),
    };
}

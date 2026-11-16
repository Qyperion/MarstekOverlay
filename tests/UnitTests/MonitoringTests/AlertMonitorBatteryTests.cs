using FluentAssertions;

using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;


public class AlertMonitorBatteryTests : AlertMonitorTests
{
    [Fact]
    public void Update_BatteryHotterThanAllowed_RaisesOnceAndNamesTheLimit()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now).WithBattery(Now, temperatureCelsius: 44));

        // Act
        var hot = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 46));
        var hotter = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 47));

        // Assert
        var alert = new BatteryTemperatureAlert(Celsius: 46, LimitCelsius: 45);
        alert.IsTooHot.Should().BeTrue();
        hot.Raised.Should().Equal(alert);
        hotter.Raised.Should().BeEmpty();
        hotter.Active.Should().Equal(alert with { Celsius = 47 });
    }

    [Fact]
    public void Update_BatteryColderThanAllowed_RaisesWithTheLowerLimit()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now).WithBattery(Now, temperatureCelsius: 10));

        // Act
        var report = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 3));

        // Assert
        var alert = report.Raised.Should().ContainSingle().Which.Should().BeOfType<BatteryTemperatureAlert>().Subject;
        alert.Should().Be(new BatteryTemperatureAlert(Celsius: 3, LimitCelsius: 5));
        alert.IsTooHot.Should().BeFalse();
    }

    [Fact]
    public void Update_TemperatureHoversAtTheLimit_RaisesAgainOnlyAfterItWasWellWithin()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now).WithBattery(Now, temperatureCelsius: 40));
        UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 45.5));

        // Act
        var justWithin = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 44.5));
        var overAgain = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 45.5));
        UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 43));
        var overOnceMore = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 45.5));

        // Assert
        justWithin.Active.Should().BeEmpty();
        overAgain.Raised.Should().BeEmpty();
        overOnceMore.Raised.Should().ContainSingle();
    }

    [Fact]
    public void Update_BatteryAlreadyTooHotAtLaunch_ShowsItWithoutRaising()
    {
        // Act
        var report = Update(StationReadings.HoldingFull(Now).WithBattery(Now, temperatureCelsius: 50));

        // Assert
        report.Raised.Should().BeEmpty();
        report.Active.Should().Equal(new BatteryTemperatureAlert(Celsius: 50, LimitCelsius: 45));
    }

    [Fact]
    public void Update_DischargingAlreadyForbiddenAtLaunch_RaisesBecauseItIsCritical()
    {
        // Act
        var report = Update(StationReadings.HoldingFull(Now).WithBattery(Now, isDischargingAllowed: false));
        var next = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, isDischargingAllowed: false));

        // Assert
        var alert = new FlowForbiddenAlert(BatteryFlow.Discharging);
        alert.Severity.Should().Be(AlertSeverity.Critical);
        report.Raised.Should().Equal(alert);
        next.Raised.Should().BeEmpty();
        next.Active.Should().Equal(alert);
    }

    [Fact]
    public void Update_ChargingAlreadyForbiddenAtLaunch_ShowsItWithoutRaising()
    {
        // Act
        var report = Update(StationReadings.HoldingFull(Now).WithBattery(Now, isChargingAllowed: false));

        // Assert
        report.Raised.Should().BeEmpty();
        report.Active.Should().Equal(new FlowForbiddenAlert(BatteryFlow.Charging));
    }

    [Fact]
    public void Update_ChargingForbiddenWhileWatching_RaisesEachTimeItSetsIn()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now).WithBattery(Now));

        // Act
        var forbidden = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, isChargingAllowed: false));
        var stillForbidden = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, isChargingAllowed: false));
        var allowed = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at));
        var forbiddenAgain = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, isChargingAllowed: false));

        // Assert
        forbidden.Raised.Should().Equal(new FlowForbiddenAlert(BatteryFlow.Charging));
        stillForbidden.Raised.Should().BeEmpty();
        allowed.Active.Should().BeEmpty();
        forbiddenAgain.Raised.Should().Equal(new FlowForbiddenAlert(BatteryFlow.Charging));
    }

    [Fact]
    public void Update_BatteryAnswerGoneOld_KeepsNoWarningMadeOfIt()
    {
        // Arrange
        var answeredAt = Now;
        StationSnapshot Read(DateTimeOffset at) => StationReadings.HoldingFull(at)
            .WithBattery(answeredAt, temperatureCelsius: 50, isChargingAllowed: false);
        var fresh = Update(Read(Now));

        // Act
        Time.Advance((SlowPollInterval + PollClock.Interval) * 3);
        var stillTrusted = Update(Read(Now));
        var tooOld = UpdateAfterInterval(Read);

        // Assert
        fresh.Active.Should().HaveCount(2);
        stillTrusted.Active.Should().HaveCount(2);
        tooOld.Active.Should().BeEmpty();
        tooOld.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_BatteryAnswersAgainWithTheSameTrouble_ShowsItWithoutRaisingItAnew()
    {
        // Arrange
        var answeredAt = Now;
        Update(StationReadings.HoldingFull(Now).WithBattery(Now, temperatureCelsius: 40));
        UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 50));
        Time.Advance(SlowPollInterval * 4);
        var silent = Update(StationReadings.HoldingFull(Now).WithBattery(answeredAt + PollClock.Interval, temperatureCelsius: 50));

        // Act
        var answered = UpdateAfterInterval(at => StationReadings.HoldingFull(at).WithBattery(at, temperatureCelsius: 50));

        // Assert
        silent.Active.Should().BeEmpty();
        answered.Active.Should().Equal(new BatteryTemperatureAlert(Celsius: 50, LimitCelsius: 45));
        answered.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_SeveralThingsWrong_ListsTheGravestFirst()
    {
        // Act
        var report = Update(StationReadings.OnBattery(Now, soc: 40)
            .WithBattery(Now, temperatureCelsius: 50, isChargingAllowed: false, isDischargingAllowed: false));

        // Assert
        report.Active.Select(alert => alert.Severity).Should().Equal(
            AlertSeverity.Critical,
            AlertSeverity.Warning,
            AlertSeverity.Warning,
            AlertSeverity.Warning);
        report.Active[0].Should().Be(new FlowForbiddenAlert(BatteryFlow.Discharging));
    }
}

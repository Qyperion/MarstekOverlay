using FluentAssertions;

using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;


public class AlertMonitorChargeTests : AlertMonitorTests
{
    [Fact]
    public void Update_ChargeComesDownToAThreshold_RaisesItOnce()
    {
        // Arrange
        BeginOnBattery(soc: 52);

        // Act
        var above = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 51));
        var reached = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 50));
        var unchanged = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 50));
        var lower = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 49));

        // Assert
        var alert = new LowChargeAlert(ThresholdPercent: 50, SocPercent: 50, IsLowestThreshold: false);
        above.Raised.Should().BeEmpty();
        above.Active.Should().BeEmpty();
        reached.Raised.Should().Equal(alert);
        reached.Active.Should().Equal(alert);
        unchanged.Raised.Should().BeEmpty();
        lower.Raised.Should().BeEmpty();
        lower.Active.Should().Equal(alert with { SocPercent = 49 });
    }

    [Fact]
    public void Update_SameAnswerAgain_RaisesNothingMore()
    {
        // Arrange
        Update(StationReadings.OnBattery(Now, soc: 51));
        Time.NextPoll();
        var failedPoll = StationReadings.OnBattery(Now, soc: 50);
        Update(failedPoll);

        // Act
        var report = UpdateAfterInterval(_ => failedPoll);

        // Assert
        report.Raised.Should().BeEmpty();
        report.Active.Should().ContainSingle();
    }

    [Fact]
    public void Update_ChargeHoversAtAThreshold_DoesNotRaiseItAgain()
    {
        // Arrange
        Update(StationReadings.OnBattery(Now, soc: 51));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 50));

        // Act
        var justAbove = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 51));
        var backDown = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 50));

        // Assert
        justAbove.Active.Should().BeEmpty();
        backDown.Raised.Should().NotContain(alert => alert is LowChargeAlert);
        backDown.Active.Should().ContainSingle();
    }

    [Fact]
    public void Update_ChargeBackAboveTheThresholdWithAMargin_RaisesItAgainOnTheNextFall()
    {
        // Arrange
        Update(StationReadings.OnBattery(Now, soc: 51));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 50));
        UpdateAfterInterval(at => StationReadings.Charging(at, soc: 52));

        // Act
        var report = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 50));

        // Assert
        report.Raised.Should().Contain(new LowChargeAlert(ThresholdPercent: 50, SocPercent: 50, IsLowestThreshold: false));
    }

    [Fact]
    public void Update_SeveralThresholdsPassedBetweenTwoAnswers_RaisesTheLowestOfThem()
    {
        // Arrange
        BeginOnBattery(soc: 60);

        // Act
        var report = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 25));
        var next = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 25));

        // Assert
        report.Raised.Should().Equal(new LowChargeAlert(ThresholdPercent: 30, SocPercent: 25, IsLowestThreshold: false));
        report.Active.Should().Equal(report.Raised);
        next.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_ChargeAlreadyLowAtLaunch_ShowsItWithoutRaising()
    {
        // Act
        var report = Update(StationReadings.OnBattery(Now, soc: 40));

        // Assert
        report.Raised.Should().BeEmpty();
        report.Active.Should().Equal(new LowChargeAlert(ThresholdPercent: 50, SocPercent: 40, IsLowestThreshold: false));
    }

    [Fact]
    public void Update_LowChargeWhileTheGridChargesTheBattery_KeepsNoWarningUp()
    {
        // Arrange
        BeginOnBattery(soc: 31);
        var onBattery = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 30));

        // Act
        var charging = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 30));
        var stillLow = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 31));
        var outageAgain = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 31));

        // Assert
        var alert = new LowChargeAlert(ThresholdPercent: 30, SocPercent: 30, IsLowestThreshold: false);
        onBattery.Active.Should().Equal(alert);
        charging.Active.Should().BeEmpty();
        stillLow.Active.Should().BeEmpty();
        outageAgain.Active.Should().Equal(new LowChargeAlert(ThresholdPercent: 50, SocPercent: 31, IsLowestThreshold: false));
        outageAgain.Raised.Should().BeEmpty("the charge has not been back above the threshold with a margin");
    }

    [Fact]
    public void Update_ThresholdPassedDownWhileCharging_IsStillRaised()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 51));

        // Act
        var report = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 50));

        // Assert
        report.Raised.Should().Equal(new LowChargeAlert(ThresholdPercent: 50, SocPercent: 50, IsLowestThreshold: false));
        report.Active.Should().BeEmpty();
    }

    [Fact]
    public void Update_ChargeAlreadyCriticalAtLaunch_RaisesTheLowestThresholdOnly()
    {
        // Act
        var report = Update(StationReadings.OnBattery(Now, soc: 12));
        var next = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 12));

        // Assert
        var alert = new LowChargeAlert(ThresholdPercent: 15, SocPercent: 12, IsLowestThreshold: true);
        alert.Severity.Should().Be(AlertSeverity.Critical);
        report.Raised.Should().Equal(alert);
        report.Active.Should().Equal(alert);
        next.Raised.Should().NotContain(raised => raised is LowChargeAlert);
    }

    [Fact]
    public void Update_ChargeAlreadyCriticalAtLaunchButCharging_RaisesNothing()
    {
        // Act
        var report = Update(StationReadings.Charging(Now, soc: 12));
        var next = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 13));

        // Assert
        report.Raised.Should().BeEmpty();
        report.Active.Should().BeEmpty();
        next.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_ChargeCriticalAgainAfterChargingAtLaunch_RaisesItOnTheNextFall()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 12));
        UpdateAfterInterval(at => StationReadings.Charging(at, soc: 17));

        // Act
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 16));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 16));
        var report = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 15));

        // Assert
        report.Raised.Should().Equal(new LowChargeAlert(ThresholdPercent: 15, SocPercent: 15, IsLowestThreshold: true));
    }

    [Fact]
    public void Update_LowChargeSwitchedOnLater_DoesNotRaiseWhatWasAlreadySo()
    {
        // Arrange
        Settings.Watch(AlertKind.LowCharge, isWatched: false);
        BeginOnBattery(soc: 60);
        var whileOff = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 40));

        // Act
        Settings.Watch(AlertKind.LowCharge, isWatched: true);
        var switchedOn = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 40));
        var nextThreshold = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 30));

        // Assert
        whileOff.Raised.Should().BeEmpty();
        whileOff.Active.Should().BeEmpty();
        switchedOn.Raised.Should().BeEmpty();
        switchedOn.Active.Should().ContainSingle();
        nextThreshold.Raised.Should().Equal(new LowChargeAlert(ThresholdPercent: 30, SocPercent: 30, IsLowestThreshold: false));
    }

    [Fact]
    public void Update_ThresholdAddedAboveTheCharge_ShowsItWithoutRaising()
    {
        // Arrange
        BeginOnBattery(soc: 70);

        // Act
        Settings.ChargeThresholdsPercent = [80, 30, 15];
        var report = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 70));

        // Assert
        report.Raised.Should().BeEmpty();
        report.Active.Should().Equal(new LowChargeAlert(ThresholdPercent: 80, SocPercent: 70, IsLowestThreshold: false));
    }
}

using FluentAssertions;

using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;


public class AlertMonitorSupplyTests : AlertMonitorTests
{
    [Fact]
    public void Update_MainsFoundAtLaunch_RaisesNothing()
    {
        // Act
        var charging = Update(StationReadings.Charging(Now, soc: 90));
        var full = UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        charging.Raised.Should().BeEmpty();
        full.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_OutageWhileOnMains_RaisesMainsLostOnceItIsConfirmed()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 90));

        // Act
        var afterOneAnswer = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        var afterTwoAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
        var afterThreeAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Assert
        afterOneAnswer.Raised.Should().BeEmpty();
        afterTwoAnswers.Raised.Should().Equal(new MainsLostAlert(SocPercent: 89));
        afterThreeAnswers.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_MainsBackAfterAWatchedOutage_TellsOfTheOutageThatEnded()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 90));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 240, soc: 90));
        for (var poll = 0; poll < 4; poll++)
            UpdateAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 240, soc: 89));

        // Act
        var report = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 89));

        // Assert
        var restored = report.Raised.Should().ContainSingle().Which.Should().BeOfType<MainsRestoredAlert>().Subject;
        restored.SocPercent.Should().Be(89);
        restored.Outage.Should().NotBeNull().And.Be(Steps.Journal.LatestFinished);
        restored.Outage.Duration.Should().Be(PollClock.Interval * 5);
        restored.Outage.IsEndObserved.Should().BeTrue();
        // 240 W over the four intervals up to the last answer on the battery.
        restored.Outage.ConsumedEnergy.WattHours.Should().BeApproximately(8, WattHourPrecision);
    }

    [Fact]
    public void Update_AlreadyOnBatteryAtLaunch_TellsOnceThatTheStationWasFoundSo()
    {
        // Act
        var afterOneAnswer = Update(StationReadings.OnBattery(Now, soc: 80));
        var afterTwoAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 79));
        var afterThreeAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 79));

        // Assert
        Steps.Tracker.Supply.Source.Should().Be(PowerSource.Battery);
        afterOneAnswer.Raised.Should().BeEmpty();
        afterTwoAnswers.Raised.Should().Equal(new MainsLostAlert(SocPercent: 79, IsSwitchSeen: false));
        afterThreeAnswers.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_OutageCarriedOverARestart_IsToldOfOncePerRunAndSoIsItsEnd()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 90));
        Time.NextPoll();
        var startedAt = Now;
        Update(StationReadings.OnBattery(Now, soc: 90));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Act
        Restart();
        Time.Advance(TimeSpan.FromMinutes(2));
        var stillOnBattery = Update(StationReadings.OnBattery(Now, soc: 88));
        var nextAnswer = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 88));
        var mainsBack = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 88));

        // Assert
        stillOnBattery.Raised.Should().Equal(new MainsLostAlert(SocPercent: 88, IsSwitchSeen: false));
        nextAnswer.Raised.Should().BeEmpty();
        var restored = mainsBack.Raised.Should().ContainSingle().Which.Should().BeOfType<MainsRestoredAlert>().Subject;
        restored.Outage.Should().NotBeNull();
        restored.Outage.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public void Update_OutageCarriedOverARestartButNoAnswerYet_WaitsForTheStationToConfirmIt()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 90));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Act
        Restart();
        Time.Advance(TimeSpan.FromMinutes(2));
        var silent = Update(StationSnapshot.Empty);
        var answered = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 88));

        // Assert
        Steps.Tracker.Supply.Source.Should().Be(PowerSource.Battery);
        silent.Raised.Should().BeEmpty();
        answered.Raised.Should().Equal(new MainsLostAlert(SocPercent: 88, IsSwitchSeen: false));
    }

    [Fact]
    public void Update_MainsFoundAtLaunchThenAnOutage_TellsOfTheSwitchAlone()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now));

        // Act
        var reports = Enumerable.Range(0, 5).Select(_ => UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99))).ToList();

        // Assert
        reports[..^1].Should().OnlyContain(report => report.Raised.Count == 0);
        reports[^1].Raised.Should().Equal(new MainsLostAlert(SocPercent: 99));
    }

    [Fact]
    public void Update_FoundOnBatteryAfterALongSilenceInTheSameRun_TellsThatTheStationWasFoundSo()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now));
        Time.Advance(PowerSourceTracker.LongestUnwatchedGap + PollClock.Interval);

        // Act
        var afterOneAnswer = Update(StationReadings.OnBattery(Now, soc: 70));
        var afterTwoAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 70));
        var afterThreeAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 70));

        // Assert
        Steps.Tracker.Supply.Should().Be(new PowerSupply(PowerSource.Battery, Now - PollClock.Interval * 2));
        afterOneAnswer.Raised.Should().BeEmpty();
        afterTwoAnswers.Raised.Should().Equal(new MainsLostAlert(SocPercent: 70, IsSwitchSeen: false));
        afterThreeAnswers.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_StillOnBatteryAfterTheComputerSlept_TellsOfTheOutageAgain()
    {
        // Arrange
        BeginOnBattery(soc: 80);
        Time.Advance(TimeSpan.FromMinutes(11));

        // Act
        var afterOneAnswer = Update(StationReadings.OnBattery(Now, soc: 70));
        var afterTwoAnswers = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 70));

        // Assert
        afterOneAnswer.Raised.Should().BeEmpty();
        afterTwoAnswers.Raised.Should().Equal(new MainsLostAlert(SocPercent: 70, IsSwitchSeen: false));
    }

    [Fact]
    public void Update_MainsFoundAfterALongSilenceThenAnOutage_TellsOfTheSwitchAlone()
    {
        // Arrange
        BeginOnBattery(soc: 80);
        Time.Advance(TimeSpan.FromMinutes(11));
        Update(StationReadings.Charging(Now, soc: 70));

        // Act
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 70));
        var report = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 70));

        // Assert
        report.Raised.Should().Equal(new MainsLostAlert(SocPercent: 70));
    }

    [Fact]
    public void Update_MainsLostSwitchedOffAtLaunch_IsNotToldOfTheOutageEvenOnceSwitchedOn()
    {
        // Arrange
        Settings.Watch(AlertKind.MainsLost, isWatched: false);
        Update(StationReadings.OnBattery(Now, soc: 80));
        var found = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 80));

        // Act
        Settings.Watch(AlertKind.MainsLost, isWatched: true);
        var switchedOn = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 80));

        // Assert
        found.Raised.Should().BeEmpty();
        switchedOn.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_MainsFoundBackAfterALongSilence_RaisesNothing()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 90));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Act
        Time.Advance(PowerSourceTracker.LongestUnwatchedGap + PollClock.Interval);
        var report = Update(StationReadings.Charging(Now, soc: 60));

        // Assert
        Steps.Tracker.Supply.Source.Should().Be(PowerSource.Mains);
        report.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_OutageLeftOpenByAnEarlierRun_IsNotToldOfOnceTheMainsAreFoundBack()
    {
        // Arrange
        Update(StationReadings.OnBattery(Now));
        UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Act
        Restart();
        Time.Advance(TimeSpan.FromHours(8));
        var report = Update(StationReadings.HoldingFull(Now));

        // Assert
        Steps.Journal.LatestFinished.Should().NotBeNull();
        report.Raised.Should().BeEmpty();
    }

    [Fact]
    public void Update_FullBatteryToppedUp_RaisesNothing()
    {
        // Arrange
        Update(StationReadings.Charging(Now, soc: 99));
        List<AlertReport> reports = [];

        // Act
        for (var poll = 0; poll < 5; poll++)
            reports.Add(UpdateAfterInterval(at => StationReadings.HoldingFull(at)));

        reports.Add(UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99)));
        reports.Add(UpdateAfterInterval(at => StationReadings.ToppingUp(at)));
        reports.Add(UpdateAfterInterval(at => StationReadings.HoldingFull(at)));

        // Assert
        reports.Should().OnlyContain(report => report.Raised.Count == 0 && report.Active.Count == 0);
    }

    [Fact]
    public void Update_OutageOnAFullBattery_RaisesMainsLostOnceTheChargeHasStayedDownLongerThanATopUpTakes()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now));

        // Act
        var stillFull = UpdateAfterInterval(at => StationReadings.HoldingFull(at));
        var dropped = Enumerable.Range(0, 5).Select(_ => UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99))).ToList();

        // Assert
        stillFull.Raised.Should().BeEmpty();
        dropped[..^1].Should().OnlyContain(report => report.Raised.Count == 0);
        dropped[^1].Raised.Should().Equal(new MainsLostAlert(SocPercent: 99));
        Steps.Tracker.Supply.IsSinceObserved.Should().BeFalse();
    }

    [Fact]
    public void Update_MainsAlertsSwitchedOff_RaisesNeither()
    {
        // Arrange
        Settings.Watch(AlertKind.MainsLost, isWatched: false);
        Settings.Watch(AlertKind.MainsRestored, isWatched: false);
        Update(StationReadings.Charging(Now, soc: 90));

        // Act
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        var lost = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        var restored = UpdateAfterInterval(at => StationReadings.Charging(at, soc: 90));

        // Assert
        lost.Raised.Should().BeEmpty();
        restored.Raised.Should().BeEmpty();
    }
}

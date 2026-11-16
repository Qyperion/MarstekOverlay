using FluentAssertions;

using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;


public class AlertMonitorSilenceTests : AlertMonitorTests
{
    [Fact]
    public void Update_StationSilentForLongerThanAllowed_RaisesOnce()
    {
        // Arrange
        var answeredAt = Now;
        var latestAnswer = StationReadings.HoldingFull(answeredAt);
        Update(latestAnswer);

        // Act
        List<AlertReport> reports = [];
        for (var poll = 0; poll < 12; poll++)
            reports.Add(UpdateAfterInterval(_ => latestAnswer));

        // Assert
        var alert = new StaleDataAlert(LatestAnswerAt: answeredAt);
        reports.Take(10).Should().OnlyContain(report => report.Raised.Count == 0 && report.Active.Count == 0);
        reports[10].Raised.Should().Equal(alert);
        reports[11].Raised.Should().BeEmpty();
        reports[11].Active.Should().Equal(alert);
    }

    [Fact]
    public void Update_AnswerAfterSilence_ClearsTheAlertSoThatItCanBeRaisedAgain()
    {
        // Arrange
        var latestAnswer = StationReadings.HoldingFull(Now);
        Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(4));
        Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(2));
        Update(latestAnswer);

        // Act
        latestAnswer = StationReadings.HoldingFull(Now);
        var answered = Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(4));
        Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(2));
        var silentAgain = Update(latestAnswer);

        // Assert
        answered.Active.Should().BeEmpty();
        silentAgain.Raised.Should().ContainSingle().Which.Should().BeOfType<StaleDataAlert>();
    }

    [Fact]
    public void Update_NoAnswerSinceLaunch_RaisesWithoutALatestAnswer()
    {
        // Arrange
        Update(StationSnapshot.Empty);
        Time.Advance(TimeSpan.FromMinutes(4));
        var early = Update(StationSnapshot.Empty);

        // Act
        Time.Advance(TimeSpan.FromMinutes(2));
        var report = Update(StationSnapshot.Empty);

        // Assert
        early.Raised.Should().BeEmpty();
        report.Raised.Should().Equal(new StaleDataAlert(LatestAnswerAt: null));
    }

    [Fact]
    public void Update_AppItselfWasAway_DoesNotCountThatTimeAsSilence()
    {
        // Arrange
        var latestAnswer = StationReadings.HoldingFull(Now);
        Update(latestAnswer);

        // Act
        Time.Advance(TimeSpan.FromHours(8));
        var justBack = Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(4));
        var soonAfter = Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(2));
        var stillSilent = Update(latestAnswer);

        // Assert
        justBack.Raised.Should().BeEmpty();
        justBack.Active.Should().BeEmpty();
        soonAfter.Raised.Should().BeEmpty();
        stillSilent.Raised.Should().ContainSingle().Which.Should().BeOfType<StaleDataAlert>();
    }

    [Fact]
    public void Update_PointedAtAnotherStation_WaitsForItsAnswerAfresh()
    {
        // Arrange
        Update(StationReadings.HoldingFull(Now));
        Time.Advance(TimeSpan.FromMinutes(4));
        Update(StationReadings.HoldingFull(Now));

        // Act
        Time.Advance(TimeSpan.FromMinutes(4));
        var justPointed = Update(StationSnapshot.Empty);
        Time.Advance(TimeSpan.FromMinutes(4));
        var waiting = Update(StationSnapshot.Empty);
        Time.Advance(TimeSpan.FromMinutes(2));
        var waitedTooLong = Update(StationSnapshot.Empty);

        // Assert
        justPointed.Raised.Should().BeEmpty();
        waiting.Raised.Should().BeEmpty();
        waitedTooLong.Raised.Should().Equal(new StaleDataAlert(LatestAnswerAt: null));
    }

    [Fact]
    public void Update_StaleAfterShorterThanTwoIntervals_WaitsForTwoIntervals()
    {
        // Arrange
        Settings.StaleAfterMinutes = 1;
        Restart(pollInterval: TimeSpan.FromSeconds(90));
        var answeredAt = Now;
        var latestAnswer = StationReadings.HoldingFull(answeredAt);
        Update(latestAnswer);

        // Act
        Time.Advance(TimeSpan.FromSeconds(95));
        var nextPoll = Update(latestAnswer);
        Time.Advance(TimeSpan.FromSeconds(95));
        var pollAfterThat = Update(latestAnswer);

        // Assert
        nextPoll.Raised.Should().BeEmpty();
        pollAfterThat.Raised.Should().Equal(new StaleDataAlert(LatestAnswerAt: answeredAt));
    }

    [Fact]
    public void Update_StaleDataSwitchedOff_NeverReportsIt()
    {
        // Arrange
        Settings.Watch(AlertKind.StaleData, isWatched: false);
        var latestAnswer = StationReadings.HoldingFull(Now);
        Update(latestAnswer);
        Time.Advance(TimeSpan.FromMinutes(4));
        Update(latestAnswer);

        // Act
        Time.Advance(TimeSpan.FromMinutes(4));
        var report = Update(latestAnswer);

        // Assert
        report.Should().BeEquivalentTo(new AlertReport([], []));
    }
}

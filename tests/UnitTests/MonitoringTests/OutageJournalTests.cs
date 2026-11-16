using FluentAssertions;

using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class OutageJournalTests
{
    private const double WattHourPrecision = 0.001;

    private readonly PollClock _time = new();
    private readonly InMemoryOutageStore _store = new();

    private PowerSourceTracker _tracker = new();
    private OutageJournal _journal;

    public OutageJournalTests()
    {
        _journal = new OutageJournal(_store, _tracker);
    }

    private DateTimeOffset Now => _time.Now;

    [Fact]
    public void Record_OnMains_KeepsNothing()
    {
        // Act
        Record(StationReadings.Charging(Now, soc: 99));

        for (var poll = 0; poll < 20; poll++)
            RecordAfterInterval(at => StationReadings.HoldingFull(at));

        RecordAfterInterval(at => StationReadings.ToppingUp(at));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        _journal.Ongoing.Should().BeNull();
        _store.Outages.Should().BeEmpty();
    }

    [Fact]
    public void Record_OutageConfirmed_OpensItFromTheFirstAnswerThatToldOfIt()
    {
        // Arrange
        Record(StationReadings.Charging(Now, soc: 90));
        _time.NextPoll();
        var startedAt = Now;

        // Act
        Record(StationReadings.OnBattery(Now, loadWatts: 200, soc: 90));
        var afterOneAnswer = _journal.Ongoing;
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 280, soc: 89));

        // Assert
        afterOneAnswer.Should().BeNull();
        var outage = _journal.Ongoing;
        outage.Should().NotBeNull();
        outage.StartedAt.Should().Be(startedAt);
        outage.IsStartObserved.Should().BeTrue();
        outage.EndedAt.Should().Be(Now);
        outage.IsOngoing.Should().BeTrue();
        outage.StartSocPercent.Should().Be(90);
        outage.EndSocPercent.Should().Be(89);
        // 240 W on average over 30 s.
        outage.ConsumedEnergy.WattHours.Should().BeApproximately(2, WattHourPrecision);
        _store.FindOngoing().Should().Be(outage);
    }

    [Fact]
    public void Record_OutageOnAFullBattery_IsKeptAsBegunUnseenAndCountedFromTheFirstDrop()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        _time.NextPoll();
        var noticedAt = Now;

        // Act
        Record(StationReadings.OnBattery(Now, loadWatts: 300, soc: 99));
        for (var poll = 0; poll < 4; poll++)
            RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300, soc: 99));

        // Assert
        var outage = _journal.Ongoing;
        outage.Should().NotBeNull();
        outage.StartedAt.Should().Be(noticedAt);
        outage.IsStartObserved.Should().BeFalse();
        outage.IsWhollyObserved.Should().BeFalse();
        outage.StartSocPercent.Should().Be(99);
        // 300 W over the two minutes since the charge first dropped.
        outage.ConsumedEnergy.WattHours.Should().BeApproximately(10, WattHourPrecision);
    }

    [Fact]
    public void Record_WhileOnBattery_AddsUpWhatTheBackupOutputDraws()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now, loadWatts: 300));

        // Act
        for (var poll = 0; poll < 120; poll++)
            RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300));

        // Assert
        var outage = _journal.Ongoing;
        outage.Should().NotBeNull();
        outage.Duration.Should().Be(TimeSpan.FromHours(1));
        outage.ConsumedEnergy.WattHours.Should().BeApproximately(300, WattHourPrecision);
        outage.IsStartObserved.Should().BeFalse();
        _store.Outages.Should().ContainSingle();
    }

    [Fact]
    public void Record_ReadingOlderThanTheLatest_LeavesTheOngoingOutageAsItWas()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now, loadWatts: 300));
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300));
        var ongoing = _journal.Ongoing;

        // Act
        Record(StationReadings.OnBattery(Now - TimeSpan.FromSeconds(10), loadWatts: 300));
        var afterTheOlder = _journal.Ongoing;
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300));

        // Assert
        ongoing.Should().NotBeNull();
        afterTheOlder.Should().Be(ongoing);
        var outage = _journal.Ongoing;
        outage.Should().NotBeNull();
        outage.EndedAt.Should().Be(Now);
        // 300 W over the minute between the first and the latest answer, counted once.
        outage.ConsumedEnergy.WattHours.Should().BeApproximately(5, WattHourPrecision);
    }

    [Fact]
    public void Record_MainsBack_ClosesTheOutageAtTheFirstAnswerThatShowsThem()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now, soc: 80));
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 79));
        _time.NextPoll();
        var endedAt = Now;

        // Act
        Record(StationReadings.Charging(Now, soc: 79));

        // Assert
        _journal.Ongoing.Should().BeNull();
        var outage = _journal.LatestFinished;
        outage.Should().NotBeNull();
        outage.IsOngoing.Should().BeFalse();
        outage.EndedAt.Should().Be(endedAt);
        outage.IsEndObserved.Should().BeTrue();
        outage.Duration.Should().Be(PollClock.Interval * 2);
        outage.EndSocPercent.Should().Be(79);
        _store.FindOngoing().Should().BeNull();
        _store.FindLatestFinished().Should().Be(outage);
    }

    [Fact]
    public void Record_SameAnswerAgain_AddsNothing()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        _time.NextPoll();
        var failedPoll = StationReadings.OnBattery(Now);
        Record(failedPoll);
        var before = _journal.Ongoing;

        // Act
        _time.NextPoll();
        Record(failedPoll);

        // Assert
        before.Should().NotBeNull();
        _journal.Ongoing.Should().Be(before);
    }

    [Fact]
    public void Record_AnswersStopForLong_ClosesTheOutageWhereItWasLastSeen()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        var lastSeenAt = Now;
        _time.Advance(TimeSpan.FromHours(3));
        var seenAgainAt = Now;

        // Act
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        var finished = _journal.LatestFinished;
        var ongoing = _journal.Ongoing;
        finished.Should().NotBeNull();
        ongoing.Should().NotBeNull();
        finished.EndedAt.Should().Be(lastSeenAt);
        finished.IsEndObserved.Should().BeFalse();
        ongoing.StartedAt.Should().Be(seenAgainAt);
        ongoing.IsStartObserved.Should().BeFalse();
        ongoing.Id.Should().NotBe(finished.Id);
    }

    [Fact]
    public void Restart_StillOnBatterySoonAfter_CarriesTheOutageOn()
    {
        // Arrange
        Record(StationReadings.Charging(Now));
        _time.NextPoll();
        var startedAt = Now;
        Record(StationReadings.OnBattery(Now, loadWatts: 300));
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300));

        // Act
        Restart();
        _time.Advance(TimeSpan.FromMinutes(2));
        Record(StationReadings.OnBattery(Now, loadWatts: 300));

        // Assert
        var outage = _journal.Ongoing;
        outage.Should().NotBeNull();
        outage.StartedAt.Should().Be(startedAt);
        outage.Duration.Should().Be(TimeSpan.FromMinutes(2.5));
        // 300 W for two and a half minutes, the two the app was down for included.
        outage.ConsumedEnergy.WattHours.Should().BeApproximately(12.5, WattHourPrecision);
        _tracker.Supply.Should().Be(new PowerSupply(PowerSource.Battery, startedAt, IsSinceObserved: true));
        _store.Outages.Should().ContainSingle();
    }

    [Fact]
    public void Restart_MainsBackAndBatteryRechargedMeanwhile_ClosesTheOutageWhereItWasLastSeen()
    {
        // Arrange
        Record(StationReadings.Charging(Now, soc: 81));
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 80));
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 80));
        var lastSeenAt = Now;

        // Act
        Restart();
        _time.Advance(TimeSpan.FromMinutes(5));
        Record(StationReadings.HoldingFull(Now, gridEnergyWattHours: 900));
        RecordAfterInterval(at => StationReadings.HoldingFull(at, gridEnergyWattHours: 900));

        // Assert
        _journal.Ongoing.Should().BeNull();
        var finished = _journal.LatestFinished;
        finished.Should().NotBeNull();
        finished.EndedAt.Should().Be(lastSeenAt);
        finished.IsEndObserved.Should().BeFalse();
        _tracker.Supply.Source.Should().Be(PowerSource.Mains);
    }

    [Fact]
    public void Restart_OutageStoredWithoutGridEnergyCounters_StaysOpenOnAFullBattery()
    {
        // Arrange
        Record(StationReadings.Charging(Now, soc: 81));
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 80));
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 80));
        var stored = _journal.Ongoing;
        stored.Should().NotBeNull();
        _store.Save(stored with { LatestGridEnergy = null });

        // Act
        Restart();
        _time.Advance(TimeSpan.FromMinutes(5));
        Record(StationReadings.HoldingFull(Now, gridEnergyWattHours: 900));
        RecordAfterInterval(at => StationReadings.HoldingFull(at, gridEnergyWattHours: 900));

        // Assert
        _journal.Ongoing.Should().NotBeNull();
        _journal.Ongoing.EndedAt.Should().Be(Now);
        _tracker.Supply.Source.Should().Be(PowerSource.Battery);
    }

    [Fact]
    public void Restart_MuchLater_ClosesTheOutageLeftOpen()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        var lastSeenAt = Now;

        // Act
        Restart();
        _time.Advance(TimeSpan.FromHours(8));
        Record(StationReadings.HoldingFull(Now));

        // Assert
        var finished = _journal.LatestFinished;
        finished.Should().NotBeNull();
        finished.EndedAt.Should().Be(lastSeenAt);
        finished.IsEndObserved.Should().BeFalse();
        _journal.Ongoing.Should().BeNull();
        _tracker.Supply.Source.Should().Be(PowerSource.Mains);
    }

    [Fact]
    public void Restart_KeepsTheLatestFinishedOutage()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var finished = _journal.LatestFinished;

        // Act
        Restart();

        // Assert
        finished.Should().NotBeNull();
        _journal.LatestFinished.Should().Be(finished);
        _journal.Ongoing.Should().BeNull();
    }

    [Fact]
    public void Since_ListsTheOngoingOutageAboveTheFinishedOnes()
    {
        // Arrange
        var watchedFrom = Now;
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var finished = _journal.LatestFinished;

        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        var afterTheFirst = Now - PollClock.Interval;

        // Act
        var all = _journal.Since(watchedFrom);
        var recent = _journal.Since(afterTheFirst);

        // Assert
        var ongoing = _journal.Ongoing;
        ongoing.Should().NotBeNull();
        finished.Should().NotBeNull();
        all.Should().Equal(ongoing, finished);
        recent.Should().Equal(ongoing);
    }

    [Fact]
    public void Between_ListsTheOutagesWithAPartInThePeriod()
    {
        // Arrange
        var watchedFrom = Now;
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var first = _journal.LatestFinished;
        var afterTheFirst = Now;

        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        var second = _journal.Ongoing;

        // Act & Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        _journal.Between(watchedFrom, afterTheFirst).Should().Equal(first);
        _journal.Between(afterTheFirst + PollClock.Interval, Now + PollClock.Interval).Should().Equal(second);
        _journal.Between(watchedFrom, Now + PollClock.Interval).Should().Equal(second, first);
        _journal.Between(Now + PollClock.Interval, Now + TimeSpan.FromDays(1)).Should().BeEmpty();
    }

    [Fact]
    public void FirstStartedAt_TellsWhenTheEarliestOutageBegan()
    {
        // Arrange
        _journal.FirstStartedAt().Should().BeNull();

        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        var startedAt = _journal.Ongoing?.StartedAt;
        RecordAfterInterval(at => StationReadings.Charging(at));

        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));

        // Act & Assert
        startedAt.Should().NotBeNull();
        _journal.FirstStartedAt().Should().Be(startedAt);
    }

    [Fact]
    public void DeleteFinishedBefore_LatestFinishedAmongTheDeleted_NoLongerNamesIt()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));

        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        var ongoing = _journal.Ongoing;

        // Act
        var deleted = _journal.DeleteFinishedBefore(DateTimeOffset.MaxValue);

        // Assert
        deleted.Should().Be(1);
        _journal.LatestFinished.Should().BeNull();
        ongoing.Should().NotBeNull();
        _journal.Ongoing.Should().Be(ongoing);
        _store.Outages.Should().Equal(ongoing);
    }

    [Fact]
    public void DeleteFinishedBefore_LatestFinishedEndedLater_GoesOnNamingIt()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var afterTheFirst = Now;

        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var latest = _journal.LatestFinished;

        // Act
        var deleted = _journal.DeleteFinishedBefore(afterTheFirst + PollClock.Interval);

        // Assert
        deleted.Should().Be(1);
        latest.Should().NotBeNull();
        _journal.LatestFinished.Should().Be(latest);
        _store.Outages.Should().Equal(latest);
    }

    [Fact]
    public void Record_NewOutageAfterAnOlderOneWasDeleted_KeepsTheOnesThatRemained()
    {
        // Arrange
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var afterTheFirst = Now;

        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));
        RecordAfterInterval(at => StationReadings.Charging(at));
        var remaining = _journal.LatestFinished;
        _journal.DeleteFinishedBefore(afterTheFirst + PollClock.Interval);

        // Act
        _time.Advance(TimeSpan.FromHours(1));
        Record(StationReadings.OnBattery(Now));
        RecordAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        var ongoing = _journal.Ongoing;
        remaining.Should().NotBeNull();
        ongoing.Should().NotBeNull();
        ongoing.Id.Should().NotBe(remaining.Id);
        _journal.LatestFinished.Should().Be(remaining);
        _store.Outages.Should().BeEquivalentTo([remaining, ongoing]);
    }

    private void Record(StationSnapshot snapshot) => _journal.Record(_tracker.Update(snapshot), snapshot);

    private void RecordAfterInterval(Func<DateTimeOffset, StationSnapshot> read)
    {
        Record(read(_time.NextPoll()));
    }

    private void Restart()
    {
        _tracker = new PowerSourceTracker();
        _journal = new OutageJournal(_store, _tracker);
    }
}

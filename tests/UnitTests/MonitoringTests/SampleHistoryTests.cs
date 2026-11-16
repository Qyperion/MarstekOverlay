using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Options;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class SampleHistoryTests
{

    private readonly PollClock _time = new();
    private readonly InMemorySampleStore _store = new();
    private readonly PowerSourceTracker _tracker = new();

    private SampleHistory _history;

    public SampleHistoryTests()
    {
        _history = CreateHistory();
    }

    private DateTimeOffset Now => _time.Now;

    [Fact]
    public void Record_NewAnswer_KeepsItsChargeLoadAndSource()
    {
        // Act
        Record(StationReadings.Charging(Now, loadWatts: 250, soc: 80));

        // Assert
        var expected = new StationSample(Now, 80, new Power(2200), new Power(250), PowerSource.Mains);
        _store.Samples.Should().Equal(expected);
        _history.Since(Now - PollClock.Interval).Should().Equal(expected);
    }

    [Fact]
    public void Record_FullBatteryOnMains_TakesTheLoadForTheGridsNotTheBatterys()
    {
        // Act
        Record(StationReadings.HoldingFull(Now, loadWatts: 300));

        // Assert
        _store.Samples.Should().Equal(new StationSample(Now, 100, Power.Zero, new Power(300), PowerSource.Mains));
    }

    [Fact]
    public void Record_SameAnswerAgain_AddsNothing()
    {
        // Arrange
        var failedPoll = StationReadings.HoldingFull(Now);
        Record(failedPoll);

        // Act
        _time.NextPoll();
        Record(failedPoll);

        // Assert
        _store.Samples.Should().ContainSingle();
    }

    [Fact]
    public void Record_OutageConfirmed_MarksTheSampleItBeganWith()
    {
        // Arrange
        Record(StationReadings.Charging(Now, soc: 90));
        _time.NextPoll();
        var startedAt = Now;

        // Act
        Record(StationReadings.OnBattery(Now, loadWatts: 200, soc: 90));
        var afterOneAnswer = _store.Samples[^1];
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 280, soc: 89));

        // Assert
        afterOneAnswer.Source.Should().Be(PowerSource.Mains);
        _store.Samples.Should().Equal(_history.Since(startedAt - PollClock.Interval));
        _store.Samples.Skip(1).Should().Equal(
            new StationSample(startedAt, 90, new Power(-200), new Power(200), PowerSource.Battery),
            new StationSample(Now, 89, new Power(-280), new Power(280), PowerSource.Battery));
    }

    [Fact]
    public void Record_OutageOnAFullBatteryConfirmed_MarksEverySampleSinceTheChargeDropped()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now));
        _time.NextPoll();
        var droppedAt = Now;

        // Act
        Record(StationReadings.OnBattery(Now, soc: 99));
        for (var poll = 0; poll < 4; poll++)
            RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 99));

        // Assert
        _store.Samples.Select(sample => sample.Source).Should().Equal(
            PowerSource.Mains, PowerSource.Battery, PowerSource.Battery, PowerSource.Battery, PowerSource.Battery, PowerSource.Battery);
        _store.Samples[1].At.Should().Be(droppedAt);
        _store.Samples[1].BatteryPower.Should().Be(new Power(-250));
        _history.Since(droppedAt).Should().Equal(_store.Samples.Skip(1));
    }

    [Fact]
    public void Restart_KeepsWhatWasRecordedBefore()
    {
        // Arrange
        var startedAt = Now;
        Record(StationReadings.HoldingFull(Now));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        var recorded = _store.Samples;

        // Act
        _time.Advance(TimeSpan.FromMinutes(20));
        _history = CreateHistory();
        Record(StationReadings.HoldingFull(Now));

        // Assert
        _history.Since(startedAt).Should().Equal([.. recorded, _store.Samples[^1]]);
        _store.Samples.Should().HaveCount(3);
    }

    [Fact]
    public void Constructor_SamplesOlderThanRetention_DeletesThem()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now));
        _time.Advance(TimeSpan.FromDays(6));
        Record(StationReadings.HoldingFull(Now));
        var kept = _store.Samples[^1];

        // Act
        _time.Advance(TimeSpan.FromDays(2));
        _history = CreateHistory(retentionDays: 7);

        // Assert
        _store.Samples.Should().Equal(kept);
    }

    [Fact]
    public void Record_AsTimePasses_DeletesWhatOutlivedRetention()
    {
        // Arrange
        _history = CreateHistory(retentionDays: 1);
        var step = TimeSpan.FromMinutes(30);

        // Act
        for (var poll = 0; poll < 120; poll++)
        {
            Record(StationReadings.HoldingFull(Now));
            _time.Advance(step);
        }

        // Assert
        var oldest = _store.Samples[0].At;
        oldest.Should().BeOnOrAfter(Now - TimeSpan.FromDays(1) - TimeSpan.FromHours(1) - step);
        oldest.Should().BeBefore(Now - TimeSpan.FromHours(23));
    }

    [Fact]
    public void Since_WithinTheLatestDay_AnswersFromMemory()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        var latest = _store.Samples[^1];
        var readsBefore = _store.ReadCount;

        // Act
        var samples = _history.Since(Now);

        // Assert
        samples.Should().Equal(latest);
        _store.ReadCount.Should().Be(readsBefore);
    }

    [Fact]
    public void Since_FurtherBackThanIsKeptInMemory_ReadsTheStore()
    {
        // Arrange
        var startedAt = Now;
        Record(StationReadings.HoldingFull(Now));
        _time.Advance(TimeSpan.FromDays(2));
        Record(StationReadings.HoldingFull(Now));

        // Act
        var lately = _history.Since(Now - SampleHistory.KeptInMemory);
        var everything = _history.Since(startedAt);

        // Assert
        lately.Should().ContainSingle();
        everything.Should().Equal(_store.Samples).And.HaveCount(2);
    }

    [Fact]
    public void Between_WithinTheLatestDay_AnswersFromMemoryWithBothEndsIncluded()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now));
        var from = Now;
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        var to = Now;
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        var readsBefore = _store.ReadCount;

        // Act
        var samples = _history.Between(from, to);

        // Assert
        samples.Select(sample => sample.At).Should().Equal(from, from + PollClock.Interval, to);
        _store.ReadCount.Should().Be(readsBefore);
    }

    [Fact]
    public void Between_FurtherBackThanIsKeptInMemory_ReadsOnlyThatStretchFromTheStore()
    {
        // Arrange
        var startedAt = Now;
        Record(StationReadings.HoldingFull(Now));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        _time.Advance(TimeSpan.FromDays(2));
        Record(StationReadings.HoldingFull(Now));

        // Act
        var samples = _history.Between(startedAt, startedAt + PollClock.Interval);

        // Assert
        samples.Should().Equal(_store.Samples.Take(2));
    }

    [Fact]
    public void LoadAcross_NoOutageCrossesABound_ReadsNothing()
    {
        // Arrange
        var bound = Now;
        var readsBefore = _store.ReadCount;
        Outage[] outages = [OutageRecords.Finished(bound - TimeSpan.FromHours(2), TimeSpan.FromHours(1))];

        // Act
        var load = _history.LoadAcross(outages, [bound]);

        // Assert
        load.Should().BeSameAs(RecordedLoad.None);
        _store.ReadCount.Should().Be(readsBefore);
    }

    [Fact]
    public void LoadAcross_OutageAcrossABound_SharesItsEnergyByTheLoadRecordedThroughIt()
    {
        // Arrange
        var startedAt = Now;
        Record(StationReadings.OnBattery(Now, loadWatts: 100));
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 100));
        var bound = Now;
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300));
        RecordAfterInterval(at => StationReadings.OnBattery(at, loadWatts: 300));
        var outage = OutageRecords.Finished(startedAt, Now - startedAt, wattHours: 10);

        // Act
        var load = _history.LoadAcross([outage], [bound]);

        // Assert
        load.ShareOf(outage, bound, Now).Should().BeApproximately(500.0 / 600, 0.000_1);
    }

    [Fact]
    public void DeleteBefore_RemovesTheEarlierSamplesFromTheStoreAndFromMemory()
    {
        // Arrange
        var startedAt = Now;
        Record(StationReadings.HoldingFull(Now));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        RecordAfterInterval(at => StationReadings.HoldingFull(at));
        var kept = _store.Samples[^1];

        // Act
        var deleted = _history.DeleteBefore(Now);

        // Assert
        deleted.Should().Be(2);
        _store.Samples.Should().Equal(kept);
        _history.Since(startedAt).Should().Equal(kept);
    }

    [Fact]
    public void Record_AnswerWhoseSampleWasDeleted_IsNotTakenForANewOne()
    {
        // Arrange
        var answer = StationReadings.HoldingFull(Now);
        Record(answer);
        _history.DeleteBefore(DateTimeOffset.MaxValue);

        // Act
        Record(answer);
        var afterTheSameAnswer = _store.Samples;
        RecordAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        afterTheSameAnswer.Should().BeEmpty();
        _store.Samples.Should().ContainSingle().Which.At.Should().Be(Now);
    }

    [Fact]
    public void Record_OutageConfirmedAfterItsFirstSampleWasDeleted_DoesNotBringThatSampleBack()
    {
        // Arrange
        Record(StationReadings.Charging(Now, soc: 90));
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        _history.DeleteBefore(DateTimeOffset.MaxValue);

        // Act
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Assert
        _store.Samples.Should().ContainSingle().Which.At.Should().Be(Now);
        _history.Since(Now - SampleHistory.KeptInMemory).Should().Equal(_store.Samples);
    }

    [Fact]
    public void LongestBridgedGap_FollowsThePollInterval()
    {
        // Act
        var history = CreateHistory(pollIntervalSeconds: 60);

        // Assert
        _history.LongestBridgedGap.Should().Be(TimeSpan.FromSeconds(4 * (30 + 4)));
        history.LongestBridgedGap.Should().Be(TimeSpan.FromSeconds(4 * (60 + 4)));
    }

    private SampleHistory CreateHistory(int retentionDays = 7, int pollIntervalSeconds = 30) => new(
        _store,
        Options.Create(new HistorySettings { RetentionDays = retentionDays }),
        Options.Create(new PollingSettings { IntervalSeconds = pollIntervalSeconds }),
        _time);

    private void Record(StationSnapshot snapshot) => _history.Record(_tracker.Update(snapshot), snapshot);

    private void RecordAfterInterval(Func<DateTimeOffset, StationSnapshot> read)
    {
        Record(read(_time.NextPoll()));
    }
}

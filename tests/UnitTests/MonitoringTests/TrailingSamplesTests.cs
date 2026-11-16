using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Options;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class TrailingSamplesTests
{
    private const int Columns = 100;

    private static readonly TimeSpan ThreeDays = TimeSpan.FromDays(3);

    private readonly PollClock _time = new();
    private readonly InMemorySampleStore _store = new();
    private readonly PowerSourceTracker _tracker = new();
    private readonly SampleHistory _history;

    public TrailingSamplesTests()
    {
        _history = new SampleHistory(
            _store,
            Options.Create(new HistorySettings { RetentionDays = 7 }),
            Options.Create(new PollingSettings()),
            _time);
    }

    private DateTimeOffset Now => _time.Now;

    [Fact]
    public void Constructor_PeriodBeyondTheDayInMemory_ReadsTheStore()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now, loadWatts: 100));
        _time.Advance(TimeSpan.FromDays(2));
        Record(StationReadings.HoldingFull(Now, loadWatts: 300));

        // Act
        var samples = new TrailingSamples(_history, ThreeDays, Now);

        // Assert
        samples.Chart(Now, Columns).Summary.Should().Be(new LoadSummary(new Power(100), new Power(200), new Power(300)));
    }

    [Fact]
    public void Follow_StationSilentLongerThanTheDayInMemory_DoesNotAskTheStoreAgain()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now));
        _time.Advance(TimeSpan.FromDays(2));
        var samples = new TrailingSamples(_history, ThreeDays, Now);
        var readsBefore = _store.ReadCount;

        // Act
        var news = Enumerable.Range(0, 5).Select(_ =>
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            return samples.Follow();
        }).ToList();

        // Assert
        news.Should().AllBeEquivalentTo(false);
        _store.ReadCount.Should().Be(readsBefore);
    }

    [Fact]
    public void Follow_NewAnswer_AddsItsSample()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now, loadWatts: 200));
        var samples = new TrailingSamples(_history, TimeSpan.FromHours(3), Now);
        RecordAfterInterval(at => StationReadings.HoldingFull(at, loadWatts: 400));

        // Act
        var hasNews = samples.Follow();
        var hasMoreNews = samples.Follow();

        // Assert
        hasNews.Should().BeTrue();
        hasMoreNews.Should().BeFalse();
        samples.Chart(Now, Columns).Summary.Should().Be(new LoadSummary(new Power(200), new Power(300), new Power(400)));
    }

    [Fact]
    public void Follow_OutageConfirmed_TakesTheCorrectionOfTheSampleItBeganWith()
    {
        // Arrange
        Record(StationReadings.Charging(Now, soc: 90));
        var samples = new TrailingSamples(_history, TimeSpan.FromHours(3), Now);
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        var startedAt = Now;
        samples.Follow();

        // Act
        RecordAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
        samples.Follow();

        // Assert
        samples.Chart(Now, Columns).OnBattery.Should().ContainSingle().Which.From.Should().Be(startedAt);
    }

    [Fact]
    public void Chart_PeriodMovedOn_LeavesOutWhatFellOutOfIt()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now, loadWatts: 100));
        _time.Advance(TimeSpan.FromHours(2));
        Record(StationReadings.HoldingFull(Now, loadWatts: 300));
        var samples = new TrailingSamples(_history, TimeSpan.FromHours(3), Now);

        // Act
        var chart = samples.Chart(Now + TimeSpan.FromHours(2), Columns);

        // Assert
        chart.Summary.Should().Be(new LoadSummary(new Power(300), new Power(300), new Power(300)));
    }

    [Fact]
    public void Read_SamplesDeletedMeanwhile_LeavesThemOut()
    {
        // Arrange
        Record(StationReadings.HoldingFull(Now, loadWatts: 100));
        RecordAfterInterval(at => StationReadings.HoldingFull(at, loadWatts: 300));
        var samples = new TrailingSamples(_history, TimeSpan.FromHours(3), Now);
        _history.DeleteBefore(Now);

        // Act
        samples.Read(TimeSpan.FromHours(1), Now);

        // Assert
        samples.Period.Should().Be(TimeSpan.FromHours(1));
        samples.Chart(Now, Columns).Summary.Should().Be(new LoadSummary(new Power(300), new Power(300), new Power(300)));
    }

    private void Record(StationSnapshot snapshot) => _history.Record(_tracker.Update(snapshot), snapshot);

    private void RecordAfterInterval(Func<DateTimeOffset, StationSnapshot> read)
    {
        Record(read(_time.NextPoll()));
    }
}

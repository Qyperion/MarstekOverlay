using FluentAssertions;

using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;
using MarstekOverlay.UnitTests.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class StationPipelineTests
{
    private const string StateChanged = "state";
    private const string RecordsDeleted = "records deleted";

    private readonly PollClock _time = new();
    private readonly FakeSnapshotSource _source = new();
    private readonly FakePipelineSettings _settings = new() { Station = TestStation.Endpoint };
    private readonly QueuedSynchronizationContext _home = new();
    private readonly InMemoryOutageStore _outageStore = new();
    private readonly InMemorySampleStore _sampleStore = new();
    private readonly PipelineSteps _steps;
    private readonly List<object> _events = [];

    public StationPipelineTests()
    {
        _steps = new PipelineSteps(_time, new PollingSettings(), _outageStore, _sampleStore);
    }

    private DateTimeOffset Now => _time.Now;

    [Fact]
    public void Create_StationChosen_StartsWithWhatIsKnown()
    {
        // Act
        using var pipeline = CreatePipeline();

        // Assert
        pipeline.Latest.Should().BeEquivalentTo(new StationState(
            StationSnapshot.Empty,
            PowerSupply.Unknown,
            OngoingOutage: null,
            LatestOutage: null,
            new RuntimeForecast(ForecastStatus.InsufficientData),
            Alerts: []));
    }

    [Fact]
    public void Create_NoStationChosen_HasNoState()
    {
        // Arrange
        _settings.Station = null;

        // Act
        using var pipeline = CreatePipeline();

        // Assert
        pipeline.Latest.Should().BeNull();
    }

    [Fact]
    public void Snapshot_NoStationChosen_IsLeftAlone()
    {
        // Arrange
        _settings.Station = null;
        using var pipeline = CreatePipeline();

        // Act
        Publish(StationReadings.Charging(Now));

        // Assert
        pipeline.Latest.Should().BeNull();
        _steps.Tracker.Supply.Should().Be(PowerSupply.Unknown);
        _sampleStore.Samples.Should().BeEmpty();
        _events.Should().Equal(StateChanged);
    }

    [Fact]
    public void Snapshot_OutageWhileOnMains_GoesThroughEverythingThatFollowsTheStation()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now, soc: 90));
        _time.NextPoll();
        var switchedAt = Now;

        // Act
        Publish(StationReadings.OnBattery(Now, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Assert
        var state = pipeline.Latest;
        state.Should().NotBeNull();
        state.Snapshot.Should().BeSameAs(_source.Snapshot);
        state.Supply.Should().Be(new PowerSupply(PowerSource.Battery, switchedAt, IsSinceObserved: true));
        state.OngoingOutage.Should().NotBeNull().And.Be(_outageStore.FindOngoing());
        state.OngoingOutage.StartedAt.Should().Be(switchedAt);
        state.LatestOutage.Should().BeNull();
        state.Forecast.Status.Should().Be(ForecastStatus.Collecting);
        _sampleStore.Samples.Select(sample => sample.Source)
            .Should().Equal(PowerSource.Mains, PowerSource.Battery, PowerSource.Battery);
    }

    [Fact]
    public void Snapshot_OutageWhileOnMains_RaisesTheAlertOnceAndAheadOfTheState()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        _events.Clear();

        // Act
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Assert
        _events.Should().Equal(new MainsLostAlert(SocPercent: 89), StateChanged, StateChanged);
    }

    [Fact]
    public void Snapshot_StationFoundOnBatteryAtLaunch_RaisesTheAlertOnce()
    {
        // Arrange
        using var pipeline = CreatePipeline();

        // Act
        Publish(StationReadings.OnBattery(Now, soc: 70));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 70));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 69));

        // Assert
        _events.Should().Equal(
            StateChanged,
            new MainsLostAlert(SocPercent: 70, IsSwitchSeen: false),
            StateChanged,
            StateChanged);
    }

    [Fact]
    public void Snapshot_MainsBack_TellsOfTheOutageThatEnded()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 89));

        // Act
        PublishAfterInterval(at => StationReadings.Charging(at, soc: 89));

        // Assert
        var state = pipeline.Latest;
        state.Should().NotBeNull();
        var ended = state.LatestOutage;
        ended.Should().NotBeNull();
        ended.IsOngoing.Should().BeFalse();
        ended.Duration.Should().Be(PollClock.Interval * 2);
        state.OngoingOutage.Should().BeNull();
        state.Supply.Source.Should().Be(PowerSource.Mains);
        _events.OfType<MainsRestoredAlert>().Should().ContainSingle().Which.Outage.Should().Be(ended);
    }

    [Fact]
    public void Snapshot_LowCharge_StaysAmongTheAlertsWithoutBeingRaisedAgain()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.OnBattery(Now, soc: 31));

        // Act
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 30));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 30));

        // Assert
        var lowCharge = new LowChargeAlert(ThresholdPercent: 30, SocPercent: 30, IsLowestThreshold: false);
        (pipeline.Latest?.Alerts).Should().Equal(lowCharge);
        _events.OfType<LowChargeAlert>().Should().Equal(lowCharge);
    }

    [Fact]
    public void Snapshot_PollThatBroughtNothingNew_IsStillReportedButCountedOnce()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        var failedPoll = StationReadings.HoldingFull(Now);
        Publish(failedPoll);

        // Act
        _time.NextPoll();
        Publish(failedPoll);

        // Assert
        _events.Should().Equal(StateChanged, StateChanged);
        _sampleStore.Samples.Should().ContainSingle();
    }

    [Fact]
    public void Snapshot_FromAnotherThread_WaitsToBeTakenOnTheHomeContext()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        var charging = StationReadings.Charging(Now);
        var poll = new Thread(() => _source.Publish(charging));
        int? takenOn = null;
        pipeline.StateChanged += () => takenOn = Environment.CurrentManagedThreadId;

        // Act
        poll.Start();
        poll.Join();
        var supplyBeforeItsTurn = _steps.Tracker.Supply;
        var stateBeforeItsTurn = pipeline.Latest?.Snapshot;
        _home.RunPosted();

        // Assert
        supplyBeforeItsTurn.Should().Be(PowerSupply.Unknown);
        stateBeforeItsTurn.Should().BeSameAs(StationSnapshot.Empty);
        takenOn.Should().Be(Environment.CurrentManagedThreadId);
        (pipeline.Latest?.Snapshot).Should().BeSameAs(charging);
    }

    [Fact]
    public void SettingsChanged_AlertKindSwitchedOff_DropsItsWarningWithoutWaitingForAPoll()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.OnBattery(Now, soc: 20));
        var before = pipeline.Latest?.Alerts;
        _events.Clear();

        // Act
        _settings.Change(settings => settings.Alerts.Watch(AlertKind.LowCharge, isWatched: false));

        // Assert
        before.Should().ContainSingle().Which.Should().BeOfType<LowChargeAlert>();
        (pipeline.Latest?.Alerts).Should().BeEmpty();
        _events.Should().Equal(StateChanged);
    }

    [Fact]
    public void SettingsChanged_SameSnapshot_IsNotCountedTwice()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now));
        PublishAfterInterval(at => StationReadings.OnBattery(at));
        PublishAfterInterval(at => StationReadings.OnBattery(at));
        var before = pipeline.Latest;

        // Act
        _settings.Change(settings => settings.Forecast.ReserveSocPercent = 20);

        // Assert
        pipeline.Latest.Should().BeEquivalentTo(before);
        _sampleStore.Samples.Should().HaveCount(3);
        _events.OfType<Alert>().Should().ContainSingle();
    }

    [Fact]
    public void SettingsChanged_HigherReserve_BringsTheForecastForwardWithoutWaitingForAPoll()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.OnBattery(Now));
        PublishAfterInterval(at => StationReadings.OnBattery(at));
        PublishAfterInterval(at => StationReadings.OnBattery(at));
        var before = pipeline.Latest?.Forecast ?? default;

        // Act
        _settings.Change(settings => settings.Forecast.ReserveSocPercent = 40);

        // Assert
        var after = pipeline.Latest?.Forecast ?? default;
        before.Status.Should().Be(ForecastStatus.TimeToReserve);
        after.Status.Should().Be(ForecastStatus.TimeToReserve);
        after.EndsAt.Should().BeBefore(before.EndsAt ?? DateTimeOffset.MinValue);
        _sampleStore.Samples.Should().HaveCount(3);
    }

    [Fact]
    public void SettingsChanged_StationChosen_StartsWaitingForItsAnswers()
    {
        // Arrange
        _settings.Station = null;
        using var pipeline = CreatePipeline();

        // Act
        _settings.Change(settings => settings.Station = TestStation.Endpoint);

        // Assert
        (pipeline.Latest?.Snapshot).Should().BeSameAs(StationSnapshot.Empty);
        _events.Should().Equal(StateChanged);
    }

    [Fact]
    public void DeleteBefore_LatestOutageAmongTheDeleted_LeavesTheStateWithoutItAndTellsTheReadersFirst()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
        PublishAfterInterval(at => StationReadings.Charging(at, soc: 89));
        var before = pipeline.Latest?.LatestOutage;
        _events.Clear();

        // Act
        var deleted = pipeline.DeleteBefore(DateTimeOffset.MaxValue, RecordKinds.Outages);

        // Assert
        before.Should().NotBeNull();
        deleted.Should().Be(new DeletedRecords(Samples: null, Outages: 1));
        pipeline.Latest.Should().NotBeNull();
        (pipeline.Latest?.LatestOutage).Should().BeNull();
        _outageStore.Outages.Should().BeEmpty();
        _events.Should().Equal(RecordsDeleted, StateChanged);
    }

    [Fact]
    public void DeleteBefore_EverySample_DoesNotRecordTheSameAnswerAgain()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now));
        PublishAfterInterval(at => StationReadings.Charging(at));
        _events.Clear();

        // Act
        var deleted = pipeline.DeleteBefore(DateTimeOffset.MaxValue, RecordKinds.Samples);

        // Assert
        deleted.Should().Be(new DeletedRecords(Samples: 2, Outages: null));
        _sampleStore.Samples.Should().BeEmpty();
        _events.Should().Equal(RecordsDeleted, StateChanged);
    }

    [Fact]
    public void DeleteBefore_PartOfTheHistory_KeepsWhatWasRecordedLater()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now));
        PublishAfterInterval(at => StationReadings.Charging(at));

        // Act
        var deleted = pipeline.DeleteBefore(Now, RecordKinds.Samples);

        // Assert
        deleted.Samples.Should().Be(1);
        _sampleStore.Samples.Should().ContainSingle().Which.At.Should().Be(Now);
    }

    [Fact]
    public void DeleteBefore_BothKinds_TellsTheReadersOnce()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 90));
        PublishAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
        PublishAfterInterval(at => StationReadings.Charging(at, soc: 89));
        _events.Clear();

        // Act
        var deleted = pipeline.DeleteBefore(DateTimeOffset.MaxValue, RecordKinds.Samples | RecordKinds.Outages);

        // Assert
        deleted.Should().Be(new DeletedRecords(Samples: 4, Outages: 1));
        _events.Should().Equal(RecordsDeleted, StateChanged);
    }

    [Fact]
    public void DeleteBefore_NothingAskedFor_DeletesNothingAndTellsNobody()
    {
        // Arrange
        using var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now));
        _events.Clear();

        // Act
        var deleted = pipeline.DeleteBefore(DateTimeOffset.MaxValue, RecordKinds.None);

        // Assert
        deleted.Should().Be(new DeletedRecords(Samples: null, Outages: null));
        _sampleStore.Samples.Should().ContainSingle();
        _events.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_StopsListeningAndDropsWhatWasOnItsWay()
    {
        // Arrange
        var pipeline = CreatePipeline();
        Publish(StationReadings.Charging(Now));
        var before = pipeline.Latest;
        _events.Clear();
        _source.Publish(StationReadings.OnBattery(Now + PollClock.Interval));

        // Act
        pipeline.Dispose();
        _home.RunPosted();
        _settings.Change(settings => settings.Station = null);

        // Assert
        _source.HasListeners.Should().BeFalse();
        pipeline.Latest.Should().BeSameAs(before);
        _events.Should().BeEmpty();
    }

    private StationPipeline CreatePipeline()
    {
        var pipeline = _steps.Join(_source, _settings, _home);

        pipeline.AlertRaised += _events.Add;
        pipeline.StateChanged += () => _events.Add(StateChanged);
        pipeline.RecordsDeleted += () => _events.Add(RecordsDeleted);

        return pipeline;
    }

    private void Publish(StationSnapshot snapshot)
    {
        _source.Publish(snapshot);
        _home.RunPosted();
    }

    private void PublishAfterInterval(Func<DateTimeOffset, StationSnapshot> read)
    {
        Publish(read(_time.NextPoll()));
    }
}

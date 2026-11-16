using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>Everything a view model reads, put together as the app does it, with the stores in memory, the
/// settings in a folder of their own and the clock in the hands of the test. Answers reach the pipeline at once,
/// on the thread that hands them over.</summary>
internal sealed class StationHarness : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakeSnapshotSource _source = new();

    /// <param name="journal">What the journal holds from earlier runs.</param>
    /// <param name="isStationSet">False for an app that has not been pointed at a station yet.</param>
    /// <param name="station">What the monitor asks once started; nothing may be asked unless given.</param>
    public StationHarness(
        DateTimeOffset now,
        IEnumerable<Outage>? journal = null,
        bool isStationSet = true,
        IMarstekApiClient? station = null)
    {
        Time = new PollClock(now);
        Storage = new StorageSettings { RootDirectory = _directory.Path };
        Settings = new UserSettingsStore(Storage, NullLogger<UserSettingsStore>.Instance);
        if (isStationSet)
            Settings.Current.Station.Host = TestStation.Host;

        var outages = new InMemoryOutageStore();
        foreach (var outage in journal ?? [])
            outages.Save(outage);

        var steps = new PipelineSteps(Time, Polling, outages);
        Journal = steps.Journal;
        History = steps.History;
        Monitor = new StationMonitor(
            new StationReader(station ?? new UnreachableApiClient(), Options.Create(Polling), Time, NullLogger<StationReader>.Instance),
            Options.Create(Polling),
            Time,
            NullLogger<StationMonitor>.Instance);
        Pipeline = steps.Join(_source, Settings, new InlineContext());
    }

    public PollClock Time { get; }

    public PollingSettings Polling { get; } = new();

    public StorageSettings Storage { get; }

    public UserSettingsStore Settings { get; }

    public OutageJournal Journal { get; }

    public SampleHistory History { get; }

    /// <summary>Never started by the harness itself.</summary>
    public StationMonitor Monitor { get; }

    public StationPipeline Pipeline { get; }

    public DateTimeOffset Now => Time.Now;

    /// <summary>Hands over what the station answered just now.</summary>
    public void Answer(Func<DateTimeOffset, StationSnapshot> read) => _source.Publish(read(Now));

    public void AnswerAfterInterval(Func<DateTimeOffset, StationSnapshot> read) => _source.Publish(read(Time.NextPoll()));

    public void Dispose()
    {
        Pipeline.Dispose();
        Monitor.Dispose();
        _directory.Dispose();
    }

    private sealed class InlineContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }
}

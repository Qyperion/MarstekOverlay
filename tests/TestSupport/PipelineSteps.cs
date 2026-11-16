using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarstekOverlay.TestSupport;

/// <summary>The steps a snapshot goes through, put together as the app does it, over the stores given.</summary>
public sealed class PipelineSteps
{
    private readonly TimeProvider _time;

    public PipelineSteps(TimeProvider time, PollingSettings polling, IOutageStore outages, ISampleStore? samples = null)
    {
        _time = time;

        var options = Options.Create(polling);
        Tracker = new PowerSourceTracker();
        Journal = new OutageJournal(outages, Tracker);
        History = new SampleHistory(samples ?? new InMemorySampleStore(), Options.Create(new HistorySettings()), options, time);
        Alerts = new AlertMonitor(Tracker, options);
    }

    public PowerSourceTracker Tracker { get; }

    public OutageJournal Journal { get; }

    public SampleHistory History { get; }

    public AlertMonitor Alerts { get; }

    /// <param name="home">Where the pipeline handles what the source raises.</param>
    public StationPipeline Join(ISnapshotSource source, IPipelineSettings settings, SynchronizationContext home) =>
        new(
            source,
            Tracker,
            Journal,
            History,
            Alerts,
            new RuntimeForecaster(),
            settings,
            home,
            _time,
            NullLogger<StationPipeline>.Instance);
}

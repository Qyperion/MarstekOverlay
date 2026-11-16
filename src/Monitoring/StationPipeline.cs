using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

using Microsoft.Extensions.Logging;

namespace MarstekOverlay.Monitoring;

/// <summary>Takes every snapshot through all that follows the station, each building on the one before: the
/// power source, the outage journal, the history, the alerts and the forecast. None of them is thread-safe,
/// while the snapshots arrive on thread-pool threads, so each is handed over to the one context the pipeline
/// was created on. That is where the events are raised, and the only place to read the journal and the
/// history from.</summary>
public sealed class StationPipeline : IDisposable
{
    private readonly ISnapshotSource _source;
    private readonly PowerSourceTracker _powerSource;
    private readonly OutageJournal _outages;
    private readonly SampleHistory _history;
    private readonly AlertMonitor _alerts;
    private readonly RuntimeForecaster _forecaster;
    private readonly IPipelineSettings _settings;
    private readonly SynchronizationContext _home;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StationPipeline> _logger;

    private bool _isDisposed;

    /// <param name="home">The context of the thread this is called on.</param>
    public StationPipeline(
        ISnapshotSource source,
        PowerSourceTracker powerSource,
        OutageJournal outages,
        SampleHistory history,
        AlertMonitor alerts,
        RuntimeForecaster forecaster,
        IPipelineSettings settings,
        SynchronizationContext home,
        TimeProvider timeProvider,
        ILogger<StationPipeline> logger)
    {
        _source = source;
        _powerSource = powerSource;
        _outages = outages;
        _history = history;
        _alerts = alerts;
        _forecaster = forecaster;
        _settings = settings;
        _home = home;
        _timeProvider = timeProvider;
        _logger = logger;

        Take(source.Snapshot);

        source.SnapshotChanged += HandOver;
        settings.Changed += Reconsider;
    }

    /// <summary>Raised after every snapshot and every change of the settings, even one that left the state as
    /// it was.</summary>
    public event Action? StateChanged;

    /// <summary>Raised once for what has just happened or set in, ahead of <see cref="StateChanged"/>.</summary>
    public event Action<Alert>? AlertRaised;

    /// <summary>Raised when samples or outages were deleted on request, ahead of <see cref="StateChanged"/>:
    /// whoever holds a copy of them reads it again.</summary>
    public event Action? RecordsDeleted;

    /// <summary>Nothing until the user has pointed the app at a station.</summary>
    public StationState? Latest { get; private set; }

    /// <summary>Deletes, on request, what of <paramref name="kinds"/> was recorded before
    /// <paramref name="moment"/>. The outage that is still going on stays: it is the state of the station, not
    /// its history.</summary>
    public DeletedRecords DeleteBefore(DateTimeOffset moment, RecordKinds kinds)
    {
        int? samples = kinds.HasFlag(RecordKinds.Samples) ? _history.DeleteBefore(moment) : null;
        int? outages = kinds.HasFlag(RecordKinds.Outages) ? _outages.DeleteFinishedBefore(moment) : null;

        if (kinds != RecordKinds.None)
        {
            // The state names the latest outage, which may be among the deleted.
            RecordsDeleted?.Invoke();
            Take(_source.Snapshot);
        }

        return new DeletedRecords(samples, outages);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _source.SnapshotChanged -= HandOver;
        _settings.Changed -= Reconsider;
    }

    private void HandOver(StationSnapshot snapshot) => _home.Post(_ => Take(snapshot), null);

    // A threshold or a kind of alert just changed shows on what is already known, without waiting for a poll.
    private void Reconsider() => Take(_source.Snapshot);

    private void Take(StationSnapshot snapshot)
    {
        // A snapshot handed over just before the end may still be on its way here.
        if (_isDisposed)
            return;

        if (_settings.Station is null)
        {
            Latest = null;
            StateChanged?.Invoke();
            return;
        }

        var supply = _powerSource.Update(snapshot);
        _outages.Record(supply, snapshot);
        _history.Record(supply, snapshot);

        var alerts = _alerts.Update(snapshot, _outages.LatestFinished, _settings.Alerts, _timeProvider.GetUtcNow());
        var forecast = _forecaster.Update(snapshot, supply.Source, _settings.Forecast);

        Latest = new StationState(snapshot, supply, _outages.Ongoing, _outages.LatestFinished, forecast, alerts.Active);

        foreach (var alert in alerts.Raised)
        {
            _logger.LogInformation("Alert raised: {Alert}", alert);
            AlertRaised?.Invoke(alert);
        }

        StateChanged?.Invoke();
    }
}

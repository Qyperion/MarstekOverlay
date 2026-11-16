namespace MarstekOverlay.Monitoring.Supply;

/// <summary>Follows the power source from one status answer to the next and remembers since when it has been
/// what it is. The grid seen at work settles the source at once. Running on the battery is a conclusion rather
/// than a reading, so it takes two answers in a row and is dated by the first; right after a full battery, which
/// a late top-up leaves looking the same, it takes three and <see cref="PowerSourceRule.LongestTopUpDelay"/>.</summary>
public sealed class PowerSourceTracker
{
    /// <summary>Without an answer for longer than this, the source may have changed and changed back unseen.</summary>
    public static readonly TimeSpan LongestUnwatchedGap = TimeSpan.FromMinutes(10);

    private GridEnergyCounters? _latestCounters;
    private SupplyEvidence _latestGridEvidence;
    private DateTimeOffset? _watchedUntil;
    private bool _isResumed;
    private DrainingRun? _draining;

    /// <summary>Raised on the thread that calls <see cref="Update"/>, and only when the source itself changes:
    /// a period of the same source that starts over after a gap in the answers is not a change.</summary>
    public event Action<PowerSourceChange>? SourceChanged;

    public PowerSupply Supply { get; private set; } = PowerSupply.Unknown;

    /// <summary>Picks up a period that was being watched before the app restarted.</summary>
    /// <param name="latestCounters">The grid energy counters of the latest answer before the restart, which tell
    /// whether the grid charged the battery meanwhile.</param>
    public void Resume(PowerSupply supply, DateTimeOffset watchedUntil, GridEnergyCounters? latestCounters = null)
    {
        Supply = supply;
        _watchedUntil = watchedUntil;
        _latestCounters = latestCounters;
        _latestGridEvidence = SupplyEvidence.None;
        _isResumed = true;
        _draining = null;
    }

    public PowerSupply Update(StationSnapshot snapshot)
    {
        if (snapshot.EnergySystem is not { } reading || (_watchedUntil is { } until && reading.ReceivedAt <= until))
            return Supply;

        var seenAt = reading.ReceivedAt;
        var wasWatching = _watchedUntil is { } watchedUntil && seenAt - watchedUntil <= LongestUnwatchedGap;
        var known = wasWatching ? Supply : PowerSupply.Unknown;
        var evidence = PowerSourceRule.Read(reading.Value, wasWatching ? _latestCounters : null);

        // What the latest answer that told anything of the grid told: it at work or a full battery.
        var latestGridEvidence = wasWatching ? _latestGridEvidence : SupplyEvidence.None;

        // While the app was not running, whatever changed changed unseen.
        var isSwitchSeen = !_isResumed;

        var draining = evidence == SupplyEvidence.DrainingBattery
            ? (wasWatching ? _draining : null)?.Next()
                ?? new DrainingRun(seenAt, FollowsFullBattery: latestGridEvidence == SupplyEvidence.FullBattery, IsSwitchSeen: isSwitchSeen)
            : null;

        // Only a switch between two known sources was watched, and not every watched one can be dated; anything
        // else may have begun before it was seen.
        var next = evidence switch
        {
            SupplyEvidence.GridAtWork when known.Source != PowerSource.Mains =>
                new PowerSupply(PowerSource.Mains, seenAt, IsSinceObserved: known.Source == PowerSource.Battery && isSwitchSeen),

            // Too weak to call an outage over, but the likeliest reading when nothing else is known.
            SupplyEvidence.FullBattery when known.Source == PowerSource.Unknown =>
                new PowerSupply(PowerSource.Mains, seenAt),

            // The grid passed through to a full battery shows nowhere, so its loss went unseen until the charge
            // dropped.
            SupplyEvidence.DrainingBattery when known.Source != PowerSource.Battery && draining is { } run && run.IsConfirmedAt(seenAt) =>
                new PowerSupply(
                    PowerSource.Battery,
                    run.FirstSeenAt,
                    IsSinceObserved: known.Source == PowerSource.Mains && run is { IsSwitchSeen: true, FollowsFullBattery: false }),

            _ => known,
        };

        _watchedUntil = seenAt;
        _latestCounters = GridEnergyCounters.Of(reading.Value);
        _latestGridEvidence = evidence is SupplyEvidence.GridAtWork or SupplyEvidence.FullBattery ? evidence : latestGridEvidence;
        _isResumed = false;
        _draining = next.Source != PowerSource.Battery ? draining : null;

        return Switch(next, isWatched: known.Source != PowerSource.Unknown);
    }

    private PowerSupply Switch(PowerSupply next, bool isWatched)
    {
        var previous = Supply;
        Supply = next;

        if (next.Source != previous.Source)
            SourceChanged?.Invoke(new PowerSourceChange(previous, next, isWatched));

        return next;
    }

    /// <summary>Answers in a row that look like an outage, counted from the first.</summary>
    /// <param name="FollowsFullBattery">Whether a full battery came before them, as it does before a late top-up.</param>
    private sealed record DrainingRun(DateTimeOffset FirstSeenAt, bool FollowsFullBattery, bool IsSwitchSeen, int Answers = 1)
    {
        public DrainingRun Next() => this with { Answers = Answers + 1 };

        public bool IsConfirmedAt(DateTimeOffset seenAt) =>
            FollowsFullBattery
                ? Answers >= 3 && seenAt - FirstSeenAt >= PowerSourceRule.LongestTopUpDelay
                : Answers >= 2;
    }
}

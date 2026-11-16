namespace MarstekOverlay.Monitoring.History;

/// <summary>The samples of a period that ends now and moves on with the clock. Beyond the day the history keeps
/// in memory reading them goes to the file, so they are read once and then only followed by what is recorded
/// after them; while nothing new is recorded the history is not asked at all. Reads the history, and so lives
/// on the same thread.</summary>
public sealed class TrailingSamples
{
    private readonly SampleHistory _history;

    private List<StationSample> _samples = [];
    private DateTimeOffset _readAt;
    private DateTimeOffset? _followedTo;

    public TrailingSamples(SampleHistory history, TimeSpan period, DateTimeOffset now)
    {
        _history = history;
        Read(period, now);
    }

    public TimeSpan Period { get; private set; }

    /// <summary>Reads the whole period anew: for another period, or once what was read may have been deleted.</summary>
    public void Read(TimeSpan period, DateTimeOffset now)
    {
        Period = period;
        _readAt = now;
        _followedTo = _history.LatestAt;
        _samples = [.. _history.Since(now - period)];
    }

    /// <summary>Adds what the history has recorded since the samples were last read or followed.</summary>
    /// <returns>Whether anything was added or corrected.</returns>
    public bool Follow()
    {
        if (_history.LatestAt == _followedTo)
            return false;

        _followedTo = _history.LatestAt;

        // The latest sample is read again: the one before a switch to the battery is corrected after the fact,
        // together with the sample that confirms the switch.
        var known = _samples.Count > 0 ? _samples[^1] : null;
        var fresh = _history.Since(known?.At ?? _readAt);
        if (fresh.Count == 0 || (fresh.Count == 1 && fresh[0] == known))
            return false;

        if (known is not null)
            _samples.RemoveAt(_samples.Count - 1);

        _samples.AddRange(fresh);
        return true;
    }

    /// <summary>The chart of the period that ends at <paramref name="now"/>; what has fallen out of it is let go.</summary>
    /// <param name="columns">How many points across the chart are worth drawing.</param>
    public HistoryChart Chart(DateTimeOffset now, int columns)
    {
        var from = now - Period;
        _samples.RemoveAll(sample => sample.At < from);

        return _history.Chart(_samples, from, now, columns);
    }
}

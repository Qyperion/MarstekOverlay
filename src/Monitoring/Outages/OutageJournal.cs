using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring.Outages;

/// <summary>Writes every period on the battery down as an outage: when it began and ended, what the backup
/// output drew meanwhile and how far the charge went down.</summary>
public sealed class OutageJournal
{
    private readonly IOutageStore _store;
    private readonly Queue<Sample> _recentReadings = new();
    private Sample? _latestSample;

    /// <summary>An outage the previous run left open carries on if the station is seen on the battery again
    /// soon enough; the tracker decides that, so it is told where the previous run stopped watching and what the
    /// grid energy counters stood at then.</summary>
    public OutageJournal(IOutageStore store, PowerSourceTracker tracker)
    {
        _store = store;
        Ongoing = store.FindOngoing();
        LatestFinished = store.FindLatestFinished();

        if (Ongoing is { } outage)
        {
            tracker.Resume(
                new PowerSupply(PowerSource.Battery, outage.StartedAt, outage.IsStartObserved),
                watchedUntil: outage.EndedAt,
                outage.LatestGridEnergy);
        }
    }

    public Outage? Ongoing { get; private set; }

    public Outage? LatestFinished { get; private set; }

    /// <returns>The outages that were still going on at <paramref name="from"/> or began later, the latest
    /// first and the ongoing one among them.</returns>
    public IReadOnlyList<Outage> Since(DateTimeOffset from) => _store.FindSince(from);

    /// <returns>The outages that had any part between <paramref name="from"/> and <paramref name="to"/>, the
    /// latest first.</returns>
    public IReadOnlyList<Outage> Between(DateTimeOffset from, DateTimeOffset to) => _store.FindBetween(from, to);

    /// <summary>When the first outage on record began; nothing while the journal is empty.</summary>
    public DateTimeOffset? FirstStartedAt() => _store.FindEarliest()?.StartedAt;

    /// <summary>Deletes the outages that ended before <paramref name="moment"/>; the ongoing one stays.</summary>
    /// <returns>How many outages were deleted.</returns>
    public int DeleteFinishedBefore(DateTimeOffset moment)
    {
        var deleted = _store.DeleteFinishedBefore(moment);
        LatestFinished = _store.FindLatestFinished();

        return deleted;
    }

    /// <param name="supply">What the tracker made of the same snapshot.</param>
    public void Record(PowerSupply supply, StationSnapshot snapshot)
    {
        if (snapshot.EnergySystem is not { } reading || reading.ReceivedAt <= _latestSample?.At)
            return;

        var sample = new Sample(
            reading.ReceivedAt,
            reading.Value.OffgridPower,
            reading.Value.BatterySocPercent,
            GridEnergyCounters.Of(reading.Value));
        var since = supply.Source == PowerSource.Battery ? supply.Since : null;

        if (Ongoing is { } ongoing && ongoing.StartedAt != since)
        {
            LatestFinished = _store.Save(Finish(ongoing, supply));
            Ongoing = null;
        }

        if (since is { } startedAt)
            Ongoing = _store.Save(Ongoing is { } current ? Extend(current, sample) : Open(startedAt, supply.IsSinceObserved, sample));

        Remember(sample);
    }

    private Outage Open(DateTimeOffset startedAt, bool isStartObserved, Sample sample)
    {
        // A switch is confirmed some readings after the first that told of it, so the outage may already be a
        // few readings old.
        Sample[] readings = [.. _recentReadings.Where(earlier => earlier.At >= startedAt), sample];

        return new Outage
        {
            StartedAt = startedAt,
            IsStartObserved = isStartObserved,
            EndedAt = sample.At,
            IsOngoing = true,
            ConsumedEnergy = readings.Zip(readings.Skip(1), ConsumedBetween).Aggregate(default(Energy), (sum, part) => sum + part),
            StartSocPercent = readings[0].SocPercent,
            EndSocPercent = sample.SocPercent,
            LatestGridEnergy = sample.GridEnergy,
        };
    }

    private Outage Extend(Outage outage, Sample sample)
    {
        // Right after a restart there is no earlier reading: the load is taken to have been what it is now.
        var previous = _latestSample ?? sample with { At = outage.EndedAt };

        return outage with
        {
            EndedAt = sample.At,
            ConsumedEnergy = outage.ConsumedEnergy + ConsumedBetween(previous, sample),
            EndSocPercent = sample.SocPercent ?? outage.EndSocPercent,
            LatestGridEnergy = sample.GridEnergy ?? outage.LatestGridEnergy,
        };
    }

    private static Outage Finish(Outage outage, PowerSupply next)
    {
        // Only a switch to the mains that was watched tells when the outage ended. Otherwise all that is known
        // is when it was last seen.
        var endedAt = next is { Source: PowerSource.Mains, IsSinceObserved: true } ? next.Since : null;

        return outage with
        {
            IsOngoing = false,
            IsEndObserved = endedAt is not null,
            EndedAt = endedAt ?? outage.EndedAt,
        };
    }

    private static Energy ConsumedBetween(Sample from, Sample to)
    {
        if ((from.Output ?? to.Output) is not { } before || (to.Output ?? from.Output) is not { } after)
            return default;

        var average = new Power((Math.Max(before.Watts, 0) + Math.Max(after.Watts, 0)) / 2);
        var elapsed = to.At - from.At;

        return elapsed > TimeSpan.Zero ? average * elapsed : default;
    }

    // While no outage goes on, the latest readings are kept for one about to be confirmed. That takes minutes
    // from its first reading, well inside an unwatched gap.
    private void Remember(Sample sample)
    {
        _latestSample = sample;

        if (Ongoing is not null)
        {
            _recentReadings.Clear();
            return;
        }

        _recentReadings.Enqueue(sample);
        while (_recentReadings.Peek().At < sample.At - PowerSourceTracker.LongestUnwatchedGap)
            _recentReadings.Dequeue();
    }

    private sealed record Sample(DateTimeOffset At, Power? Output, double? SocPercent, GridEnergyCounters? GridEnergy);
}

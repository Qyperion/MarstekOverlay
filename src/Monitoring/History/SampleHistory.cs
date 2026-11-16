using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

using Microsoft.Extensions.Options;

namespace MarstekOverlay.Monitoring.History;

/// <summary>Takes a sample of every status answer and keeps the samples for as long as the settings say. The
/// latest day stays in memory, so a chart redrawn on every poll does not read the file each time.</summary>
public sealed class SampleHistory
{
    public const int KeptInMemoryHours = 24;

    public static readonly TimeSpan KeptInMemory = TimeSpan.FromHours(KeptInMemoryHours);

    private static readonly TimeSpan PruneEvery = TimeSpan.FromHours(1);

    private readonly ISampleStore _store;
    private readonly TimeSpan _retention;
    private readonly List<StationSample> _recent;
    private readonly List<Reading<EnergySystemStatus>> _offBattery = [];

    private DateTimeOffset _recentFrom;
    private DateTimeOffset _prunedAt;

    public SampleHistory(
        ISampleStore store,
        IOptions<HistorySettings> options,
        IOptions<PollingSettings> polling,
        TimeProvider timeProvider)
    {
        _store = store;
        _retention = TimeSpan.FromDays(options.Value.RetentionDays);
        LongestBridgedGap = ReadingFreshness.TrustedFor(MarstekMethods.EnergySystemGetStatus, polling.Value);

        var now = timeProvider.GetUtcNow();
        Prune(now);

        _recentFrom = now - KeptInMemory;
        _recent = [.. store.FindSince(_recentFrom)];
        LatestAt = _recent.LastOrDefault()?.At;
    }

    /// <summary>Samples further apart than this have a gap between them: the station did not answer or the
    /// app was not running, and nothing is known about the time in between.</summary>
    public TimeSpan LongestBridgedGap { get; }

    /// <summary>When the latest sample was taken; nothing before the first. Kept apart from the samples: the
    /// answer behind a deleted sample must not be taken for a new one.</summary>
    public DateTimeOffset? LatestAt { get; private set; }

    /// <returns>Oldest first.</returns>
    public IReadOnlyList<StationSample> Since(DateTimeOffset from)
    {
        if (from < _recentFrom)
            return _store.FindSince(from);

        var first = _recent.FindIndex(sample => sample.At >= from);

        return first < 0 ? [] : _recent.GetRange(first, _recent.Count - first);
    }

    /// <returns>The samples taken from <paramref name="from"/> to <paramref name="to"/>, both included; oldest first.</returns>
    public IReadOnlyList<StationSample> Between(DateTimeOffset from, DateTimeOffset to)
    {
        if (from < _recentFrom)
            return _store.FindBetween(from, to);

        return [.. _recent.SkipWhile(sample => sample.At < from).TakeWhile(sample => sample.At <= to)];
    }

    /// <summary>The load recorded through the outages that cross one of <paramref name="bounds"/>, which shares
    /// their energy out between the periods on either side. Read only as far as those outages reach.</summary>
    public RecordedLoad LoadAcross(IEnumerable<Outage> outages, IReadOnlyCollection<DateTimeOffset> bounds)
    {
        var crossing = outages.Where(outage => bounds.Any(bound => outage.StartedAt < bound && bound < outage.EndedAt)).ToList();
        if (crossing.Count == 0)
            return RecordedLoad.None;

        // One sample either side of the outages, for the output to be known right at their ends.
        var from = crossing.Min(outage => outage.StartedAt) - LongestBridgedGap;
        var to = crossing.Max(outage => outage.EndedAt) + LongestBridgedGap;

        return new RecordedLoad(Between(from, to), LongestBridgedGap);
    }

    /// <summary>The chart of what was recorded from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <param name="columns">How many points across the chart are worth drawing.</param>
    public HistoryChart Chart(DateTimeOffset from, DateTimeOffset to, int columns) => Chart(Since(from), from, to, columns);

    /// <param name="samples">Oldest first, read from this history earlier.</param>
    public HistoryChart Chart(IReadOnlyList<StationSample> samples, DateTimeOffset from, DateTimeOffset to, int columns) =>
        HistoryChart.Build(samples, from, to, columns, LongestBridgedGap);

    /// <summary>Deletes the samples taken before <paramref name="moment"/>, on request rather than by age.</summary>
    /// <returns>How many samples were deleted.</returns>
    public int DeleteBefore(DateTimeOffset moment)
    {
        _recent.RemoveAll(sample => sample.At < moment);

        return _store.DeleteBefore(moment);
    }

    /// <param name="supply">What the tracker made of the same snapshot.</param>
    public void Record(PowerSupply supply, StationSnapshot snapshot)
    {
        if (snapshot.EnergySystem is not { } reading || reading.ReceivedAt <= LatestAt)
            return;

        if (supply is { Source: PowerSource.Battery, Since: { } since })
            MarkOnBattery(since);

        var sample = Describe(reading, supply.Source);
        _recent.Add(sample);
        _store.Save(sample);
        LatestAt = sample.At;
        Remember(reading, supply.Source);

        Expire(sample.At);
    }

    // A switch to the battery is confirmed some answers after the first and dated by it, so the samples taken
    // meanwhile read the source otherwise. A sample deleted since is not brought back.
    private void MarkOnBattery(DateTimeOffset since)
    {
        foreach (var earlier in _offBattery.Where(earlier => earlier.ReceivedAt >= since))
        {
            var index = _recent.FindLastIndex(sample => sample.At == earlier.ReceivedAt);
            if (index < 0 || _recent[index].Source == PowerSource.Battery)
                continue;

            _recent[index] = Describe(earlier, PowerSource.Battery);
            _store.Save(_recent[index]);
        }

        _offBattery.Clear();
    }

    // The confirmation takes minutes from its first answer, well inside an unwatched gap.
    private void Remember(Reading<EnergySystemStatus> reading, PowerSource source)
    {
        if (source == PowerSource.Battery)
            return;

        _offBattery.Add(reading);
        _offBattery.RemoveAll(earlier => earlier.ReceivedAt < reading.ReceivedAt - PowerSourceTracker.LongestUnwatchedGap);
    }

    private static StationSample Describe(Reading<EnergySystemStatus> reading, PowerSource source) => new(
        reading.ReceivedAt,
        reading.Value.BatterySocPercent,
        PowerSourceRule.BatteryPower(reading.Value, source)?.Power,
        reading.Value.OffgridPower,
        source);

    private void Expire(DateTimeOffset now)
    {
        if (now - KeptInMemory > _recentFrom)
        {
            _recentFrom = now - KeptInMemory;
            _recent.RemoveAll(sample => sample.At < _recentFrom);
        }

        if (now - _prunedAt >= PruneEvery)
            Prune(now);
    }

    private void Prune(DateTimeOffset now)
    {
        _store.DeleteBefore(now - _retention);
        _prunedAt = now;
    }
}

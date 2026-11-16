using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Outages;

namespace MarstekOverlay.TestSupport;

public sealed class InMemoryOutageStore : IOutageStore
{
    private readonly List<Outage> _outages = [];
    private int _lastId;

    public IReadOnlyList<Outage> Outages => _outages;

    public Outage? FindOngoing() => _outages.Where(outage => outage.IsOngoing).MaxBy(outage => outage.StartedAt);

    public Outage? FindLatestFinished() => _outages.Where(outage => !outage.IsOngoing).MaxBy(outage => outage.StartedAt);

    public Outage? FindEarliest() => _outages.MinBy(outage => outage.StartedAt);

    public IReadOnlyList<Outage> FindSince(DateTimeOffset from) =>
        [.. _outages.Where(outage => outage.EndedAt >= from.AsStored).OrderByDescending(outage => outage.StartedAt)];

    public IReadOnlyList<Outage> FindBetween(DateTimeOffset from, DateTimeOffset to) =>
        [.. FindSince(from).Where(outage => outage.StartedAt < to.AsStored)];

    public Outage Save(Outage outage)
    {
        var stored = outage with
        {
            Id = outage.Id == 0 ? ++_lastId : outage.Id,
            StartedAt = outage.StartedAt.AsStored,
            EndedAt = outage.EndedAt.AsStored,
        };

        _outages.RemoveAll(existing => existing.Id == stored.Id);
        _outages.Add(stored);

        return stored;
    }

    public int DeleteFinishedBefore(DateTimeOffset moment) =>
        _outages.RemoveAll(outage => !outage.IsOngoing && outage.EndedAt < moment.AsStored);
}

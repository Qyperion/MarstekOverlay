using LiteDB;

using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Outages;

using Microsoft.Extensions.Logging;

using System.Linq.Expressions;

namespace MarstekOverlay.Storage;

public sealed class LiteDbOutageStore : IOutageStore
{
    private const string CollectionName = "outages";

    private readonly HistoryDatabase _database;
    private readonly ILiteCollection<OutageDocument> _outages;
    private readonly ILogger<LiteDbOutageStore> _logger;

    public LiteDbOutageStore(HistoryDatabase database, ILogger<LiteDbOutageStore> logger)
    {
        _database = database;
        _logger = logger;
        _outages = database.Collection<OutageDocument>(CollectionName);

        database.Try(
            () =>
            {
                _outages.EnsureIndex(outage => outage.StartedAtUtc);
                _outages.EnsureIndex(outage => outage.EndedAtUtc);
            },
            exception => logger.LogWarning(exception, "The outage journal could not be indexed; reading it will be slower"));
    }

    public Outage? FindOngoing() =>
        _database.Try(
            () => FindLatest(isOngoing: true),
            fallback: null,
            exception => _logger.LogWarning(exception, "The ongoing outage could not be read"));

    public Outage? FindLatestFinished() =>
        _database.Try(
            () => FindLatest(isOngoing: false),
            fallback: null,
            exception => _logger.LogWarning(exception, "The latest finished outage could not be read"));

    public Outage? FindEarliest() =>
        _database.Try(
            () => _outages.Query()
                .OrderBy(outage => outage.StartedAtUtc)
                .FirstOrDefault()
                ?.ToOutage(),
            fallback: null,
            exception => _logger.LogWarning(exception, "The earliest outage could not be read"));

    public IReadOnlyList<Outage> FindSince(DateTimeOffset from)
    {
        var fromUtc = from.UtcDateTime;

        return Find(outage => outage.EndedAtUtc >= fromUtc, from, to: null);
    }

    public IReadOnlyList<Outage> FindBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;

        return Find(outage => outage.EndedAtUtc >= fromUtc && outage.StartedAtUtc < toUtc, from, to);
    }

    public Outage Save(Outage outage)
    {
        var document = OutageDocument.From(outage);

        // A new document receives its id here.
        _database.Try(
            () => _outages.Upsert(document),
            exception => _logger.LogWarning(exception, "The outage that began at {StartedAt} was not saved", outage.StartedAt));

        return outage with { Id = document.Id, StartedAt = outage.StartedAt.AsStored, EndedAt = outage.EndedAt.AsStored };
    }

    public int DeleteFinishedBefore(DateTimeOffset moment)
    {
        var momentUtc = moment.UtcDateTime;

        return _database.Try(
            () =>
            {
                var deleted = _outages.DeleteMany(outage => !outage.IsOngoing && outage.EndedAtUtc < momentUtc);
                if (deleted > 0)
                    _logger.LogDebug("Deleted {Count} outages that ended before {Moment}", deleted, moment);

                return deleted;
            },
            fallback: 0,
            exception => _logger.LogWarning(exception, "The outages that ended before {Moment} were not deleted", moment));
    }

    private IReadOnlyList<Outage> Find(Expression<Func<OutageDocument, bool>> isWanted, DateTimeOffset from, DateTimeOffset? to) =>
        _database.Try<IReadOnlyList<Outage>>(
            () => _outages.Query()
                .Where(isWanted)
                .OrderByDescending(outage => outage.StartedAtUtc)
                .ToEnumerable()
                .Select(outage => outage.ToOutage())
                .ToList(),
            fallback: [],
            exception => _logger.LogWarning(exception, "The outages from {From} to {To} could not be read", from, to));

    private Outage? FindLatest(bool isOngoing) =>
        _outages.Query()
            .Where(outage => outage.IsOngoing == isOngoing)
            .OrderByDescending(outage => outage.StartedAtUtc)
            .FirstOrDefault()
            ?.ToOutage();
}

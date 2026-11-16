using LiteDB;

using MarstekOverlay.Monitoring.History;

using Microsoft.Extensions.Logging;

namespace MarstekOverlay.Storage;

public sealed class LiteDbSampleStore(HistoryDatabase database, ILogger<LiteDbSampleStore> logger) : ISampleStore
{
    private const string CollectionName = "samples";

    private readonly ILiteCollection<SampleDocument> _samples = database.Collection<SampleDocument>(CollectionName);

    public IReadOnlyList<StationSample> FindSince(DateTimeOffset from)
    {
        var fromUtc = from.UtcDateTime;

        return database.Try<IReadOnlyList<StationSample>>(
            () => _samples.Query()
                .Where(sample => sample.AtUtc >= fromUtc)
                .OrderBy(sample => sample.AtUtc)
                .ToEnumerable()
                .Select(sample => sample.ToSample())
                .ToList(),
            fallback: [],
            exception => logger.LogWarning(exception, "The samples since {From} could not be read", from));
    }

    public IReadOnlyList<StationSample> FindBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var (fromUtc, toUtc) = (from.UtcDateTime, to.UtcDateTime);

        return database.Try<IReadOnlyList<StationSample>>(
            () => _samples.Query()
                .Where(sample => sample.AtUtc >= fromUtc && sample.AtUtc <= toUtc)
                .OrderBy(sample => sample.AtUtc)
                .ToEnumerable()
                .Select(sample => sample.ToSample())
                .ToList(),
            fallback: [],
            exception => logger.LogWarning(exception, "The samples from {From} to {To} could not be read", from, to));
    }

    public void Save(StationSample sample) =>
        database.Try(
            () => _samples.Upsert(SampleDocument.From(sample)),
            exception => logger.LogWarning(exception, "The sample taken at {At} was not saved", sample.At));

    public int DeleteBefore(DateTimeOffset moment)
    {
        var momentUtc = moment.UtcDateTime;

        return database.Try(
            () =>
            {
                var deleted = _samples.DeleteMany(sample => sample.AtUtc < momentUtc);
                if (deleted > 0)
                    logger.LogDebug("Deleted {Count} samples taken before {Moment}", deleted, moment);

                return deleted;
            },
            fallback: 0,
            exception => logger.LogWarning(exception, "The samples taken before {Moment} were not deleted", moment));
    }
}

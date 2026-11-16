using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.TestSupport;

public sealed class InMemorySampleStore : ISampleStore
{
    private readonly SortedList<DateTimeOffset, StationSample> _samples = [];

    public IReadOnlyList<StationSample> Samples => [.. _samples.Values];

    public int ReadCount { get; private set; }

    public IReadOnlyList<StationSample> FindSince(DateTimeOffset from)
    {
        ReadCount++;
        return [.. _samples.Values.Where(sample => sample.At >= from.AsStored)];
    }

    public IReadOnlyList<StationSample> FindBetween(DateTimeOffset from, DateTimeOffset to)
    {
        ReadCount++;
        return [.. _samples.Values.Where(sample => sample.At >= from.AsStored && sample.At <= to.AsStored)];
    }

    public void Save(StationSample sample) => _samples[sample.At.AsStored] = sample with { At = sample.At.AsStored };

    public int DeleteBefore(DateTimeOffset moment)
    {
        var outdated = _samples.Keys.Where(at => at < moment.AsStored).ToList();
        foreach (var at in outdated)
            _samples.Remove(at);

        return outdated.Count;
    }
}

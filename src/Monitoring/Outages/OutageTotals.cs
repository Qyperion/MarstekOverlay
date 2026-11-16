using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Outages;

/// <summary>What the outages of a period add up to.</summary>
/// <param name="IsDurationExact">False when some outage was seen only in part, so the station ran on the battery
/// for at least <paramref name="Duration"/>.</param>
public readonly record struct OutageTotals(int Count, TimeSpan Duration, Energy ConsumedEnergy, bool IsDurationExact)
{
    public static readonly OutageTotals None = new(0, TimeSpan.Zero, default, IsDurationExact: true);

    /// <summary>An outage that reaches beyond the period counts with the part inside it. The journal keeps one
    /// figure of energy for the whole outage, which <paramref name="load"/> shares out.</summary>
    public static OutageTotals Over(IEnumerable<Outage> outages, DateTimeOffset from, DateTimeOffset to, RecordedLoad load) =>
        OutagePart.AllWithin(outages, from, to, load).Aggregate(None, (totals, part) => new OutageTotals(
                totals.Count + 1,
                totals.Duration + part.Duration,
                totals.ConsumedEnergy + part.ConsumedEnergy,
                totals.IsDurationExact && part.IsDurationExact));
}

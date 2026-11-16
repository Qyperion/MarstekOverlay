using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Outages;

/// <summary>What of an outage falls into a period: all of it, or what is left once the bounds of the period cut
/// off its start or its end.</summary>
public sealed record OutagePart(Outage Outage, DateTimeOffset StartedAt, DateTimeOffset EndedAt, Energy ConsumedEnergy)
{
    public bool BeganEarlier => StartedAt > Outage.StartedAt;

    public bool WentOnLater => EndedAt < Outage.EndedAt;

    public TimeSpan Duration => EndedAt - StartedAt;

    /// <summary>False when the part is known to have lasted longer: it begins or ends where the outage itself was
    /// not seen to.</summary>
    public bool IsDurationExact =>
        (BeganEarlier || Outage.IsStartObserved) && (WentOnLater || Outage.IsOngoing || Outage.IsEndObserved);

    /// <returns>The parts of those of <paramref name="outages"/> that fall into the period, in the same order.</returns>
    public static IEnumerable<OutagePart> AllWithin(IEnumerable<Outage> outages, DateTimeOffset from, DateTimeOffset to, RecordedLoad load) =>
        outages.Select(outage => Within(outage, from, to, load)).OfType<OutagePart>();

    /// <returns>Nothing for an outage that ended before <paramref name="from"/> or began after
    /// <paramref name="to"/>.</returns>
    public static OutagePart? Within(Outage outage, DateTimeOffset from, DateTimeOffset to, RecordedLoad load)
    {
        var startedAt = outage.StartedAt > from ? outage.StartedAt : from;
        var endedAt = outage.EndedAt < to ? outage.EndedAt : to;

        return endedAt < startedAt
            ? null
            : new OutagePart(outage, startedAt, endedAt, outage.ConsumedEnergy * load.ShareOf(outage, from, to));
    }
}

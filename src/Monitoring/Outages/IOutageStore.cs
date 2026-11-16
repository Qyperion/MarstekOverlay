namespace MarstekOverlay.Monitoring.Outages;

/// <summary>Where the outage journal is kept between runs. A store that cannot be read or written reports
/// nothing found and keeps the app going rather than throwing. Moments are kept and compared
/// to the millisecond, as <c>AsStored</c> gives them.</summary>
public interface IOutageStore
{
    Outage? FindOngoing();

    Outage? FindLatestFinished();

    /// <returns>The one that began before every other.</returns>
    Outage? FindEarliest();

    /// <returns>The outages that were still going on at <paramref name="from"/> or began later, the latest first.</returns>
    IReadOnlyList<Outage> FindSince(DateTimeOffset from);

    /// <returns>The outages that had any part between <paramref name="from"/> and <paramref name="to"/>: those
    /// that began before <paramref name="to"/> and were still going on at <paramref name="from"/> or later, the
    /// latest first.</returns>
    IReadOnlyList<Outage> FindBetween(DateTimeOffset from, DateTimeOffset to);

    /// <returns>The outage as stored: a new one comes back with its id.</returns>
    Outage Save(Outage outage);

    /// <summary>Deletes the outages that ended before <paramref name="moment"/>. One that is still going on
    /// stays, whenever it was last seen.</summary>
    /// <returns>How many outages were deleted.</returns>
    int DeleteFinishedBefore(DateTimeOffset moment);
}

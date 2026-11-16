namespace MarstekOverlay.Monitoring.History;

/// <summary>Where the samples are kept between runs. A store that cannot be read or written reports nothing
/// found and keeps the app going rather than throwing. Moments are kept and compared
/// to the millisecond, as <c>AsStored</c> gives them.</summary>
public interface ISampleStore
{
    /// <returns>Oldest first.</returns>
    IReadOnlyList<StationSample> FindSince(DateTimeOffset from);

    /// <returns>The samples taken from <paramref name="from"/> to <paramref name="to"/>, both included; oldest first.</returns>
    IReadOnlyList<StationSample> FindBetween(DateTimeOffset from, DateTimeOffset to);

    /// <summary>A sample taken at the moment of a stored one replaces it.</summary>
    void Save(StationSample sample);

    /// <returns>How many samples were deleted.</returns>
    int DeleteBefore(DateTimeOffset moment);
}

namespace MarstekOverlay.Monitoring;

public interface ISnapshotSource
{
    /// <summary>Raised on a thread-pool thread.</summary>
    event Action<StationSnapshot>? SnapshotChanged;

    StationSnapshot Snapshot { get; }
}

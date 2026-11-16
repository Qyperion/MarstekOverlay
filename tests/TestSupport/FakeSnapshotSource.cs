using MarstekOverlay.Monitoring;

namespace MarstekOverlay.TestSupport;

/// <summary>Hands a snapshot to its listeners on the thread that publishes it.</summary>
public sealed class FakeSnapshotSource : ISnapshotSource
{
    public event Action<StationSnapshot>? SnapshotChanged;

    public StationSnapshot Snapshot { get; private set; } = StationSnapshot.Empty;

    public bool HasListeners => SnapshotChanged is not null;

    public void Publish(StationSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }
}

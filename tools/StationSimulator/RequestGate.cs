namespace MarstekOverlay.StationSimulator;

/// <summary>Drops a request that comes too soon after the previous admitted one, as the real station does
/// (<c>docs/StationBehaviour.md</c>): a dropped request does not restart the wait.</summary>
/// <param name="spacing">Zero admits everything.</param>
public sealed class RequestGate(TimeSpan spacing)
{
    private DateTimeOffset? _admittedAt;

    public bool Admits(DateTimeOffset receivedAt)
    {
        if (_admittedAt is { } admittedAt && receivedAt - admittedAt < spacing)
        {
            return false;
        }

        _admittedAt = receivedAt;
        return true;
    }
}

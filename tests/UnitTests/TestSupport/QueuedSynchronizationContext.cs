using System.Collections.Concurrent;

namespace MarstekOverlay.UnitTests.TestSupport;

/// <summary>Keeps what is posted to it until a test runs it, the way a UI thread gets to it later and on its own.</summary>
internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<Action> _posted = new();

    public override void Post(SendOrPostCallback callback, object? state) => _posted.Enqueue(() => callback(state));

    public void RunPosted()
    {
        while (_posted.TryDequeue(out var callback))
            callback();
    }
}

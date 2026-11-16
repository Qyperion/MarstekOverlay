using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Time.Testing;

namespace MarstekOverlay.TestSupport;

/// <summary>A clock in the hands of the test that moves on a poll at a time.</summary>
public sealed class PollClock : FakeTimeProvider
{
    /// <summary>How often the station is polled unless the settings say otherwise.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(new PollingSettings().IntervalSeconds);

    public PollClock()
    {
    }

    public PollClock(DateTimeOffset now)
        : base(now)
    {
    }

    public DateTimeOffset Now => GetUtcNow();

    /// <returns>The moment of the next poll.</returns>
    public DateTimeOffset NextPoll()
    {
        Advance(Interval);
        return Now;
    }
}

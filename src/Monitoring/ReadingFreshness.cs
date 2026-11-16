using MarstekOverlay.Monitoring.Settings;

namespace MarstekOverlay.Monitoring;

/// <summary>How old an answer may get before the request behind it must have stopped being answered. An answer
/// lost now and then is ordinary Wi-Fi loss, so the limit is a few cycles of the poll the request goes out
/// with rather than one.</summary>
public static class ReadingFreshness
{
    // Two polls lost in a row are still ordinary. A cycle is the interval plus the poll itself, whose requests go
    // out a pause apart: seconds that make up most of a short interval's cycle.
    private const double FastPollsTrusted = 4;

    // The slow poll comes ten times less often, so fewer of its answers are waited for. It goes out with the
    // first poll past its interval, which makes it up to one fast cycle late each time.
    private const double SlowPollsTrusted = 3;

    private static readonly int FastRequests = PolledMethod.All.Count(method => method.SentWith == PollScope.Fast);

    /// <param name="method">The wire name of one of <see cref="PolledMethod.All"/>.</param>
    public static TimeSpan TrustedFor(string method, PollingSettings polling)
    {
        var fastCycle = TimeSpan.FromSeconds(polling.IntervalSeconds)
            + FastRequests * TimeSpan.FromMilliseconds(polling.RequestSpacingMs);

        return PolledMethod.Named(method).SentWith == PollScope.Full
            ? SlowPollsTrusted * (TimeSpan.FromSeconds(polling.SlowIntervalSeconds) + fastCycle)
            : FastPollsTrusted * fastCycle;
    }
}

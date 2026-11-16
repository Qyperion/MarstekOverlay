using MarstekOverlay.Monitoring.Settings;

namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>How long the station may stay silent before <see cref="AlertKind.StaleData"/> is raised: what the
/// settings say, but never less than the poll can tell from an answer on schedule.</summary>
public static class SilenceLimit
{
    // While all goes well the latest answer is never older than one interval and the few seconds a poll takes,
    // so a shorter silence is the station answering on schedule.
    private const double PollIntervalsOfShortestSilence = 2;

    /// <param name="chosenMinutes">As saved or typed; a file edited by hand may hold any number.</param>
    public static TimeSpan Of(double chosenMinutes, PollingSettings polling)
    {
        var chosen = TimeSpan.FromMinutes(
            Math.Clamp(chosenMinutes, AlertSettings.MinStaleAfterMinutes, AlertSettings.MaxStaleAfterMinutes));
        var shortest = PollIntervalsOfShortestSilence * TimeSpan.FromSeconds(polling.IntervalSeconds);

        return chosen > shortest ? chosen : shortest;
    }
}

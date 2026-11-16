using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring.History;

/// <summary>Turns samples into the lines of a chart: thinned to its columns and cut into separate runs wherever
/// nothing is known about the time in between.</summary>
internal static class ChartLines
{
    /// <param name="select">The value of the line in a sample; a sample without one is left out of the line.</param>
    /// <param name="longestBridgedGap">Samples further apart are not joined by a line.</param>
    public static IReadOnlyList<IReadOnlyList<ChartColumn>> Trace(
        IReadOnlyList<StationSample> samples,
        Func<StationSample, double?> select,
        DateTimeOffset from,
        TimeSpan columnWidth,
        TimeSpan longestBridgedGap)
    {
        var runs = new List<IReadOnlyList<ChartColumn>>();
        var run = new List<ChartPoint>();

        foreach (var sample in samples)
        {
            if (select(sample) is not { } value)
                continue;

            if (run.Count > 0 && sample.At - run[^1].At > longestBridgedGap)
            {
                runs.Add(Thin(run, from, columnWidth));
                run = [];
            }

            run.Add(new ChartPoint(sample.At, value));
        }

        if (run.Count > 0)
            runs.Add(Thin(run, from, columnWidth));

        return runs;
    }

    public static IReadOnlyList<IReadOnlyList<ChartPoint>> Averages(IReadOnlyList<IReadOnlyList<ChartColumn>> runs) =>
        [.. runs.Select(run => (IReadOnlyList<ChartPoint>)[.. run.Select(column => column.AveragePoint)])];

    public static IReadOnlyList<ChartSpan> BatterySpans(IReadOnlyList<StationSample> samples, TimeSpan longestBridgedGap)
    {
        var spans = new List<ChartSpan>();
        StationSample? previous = null;
        DateTimeOffset? startedAt = null;

        foreach (var sample in samples)
        {
            if (startedAt is { } start && previous is { } before)
            {
                var isOutOfSight = sample.At - before.At > longestBridgedGap;

                // The answer that shows the mains again ends the period; once the answers stop, all that is
                // known is when the station was last seen on the battery.
                if (isOutOfSight || sample.Source != PowerSource.Battery)
                {
                    spans.Add(new ChartSpan(start, isOutOfSight ? before.At : sample.At));
                    startedAt = null;
                }
            }

            if (sample.Source == PowerSource.Battery)
                startedAt ??= sample.At;

            previous = sample;
        }

        if (startedAt is { } open && previous is { } latest)
            spans.Add(new ChartSpan(open, latest.At));

        return spans;
    }

    private static IReadOnlyList<ChartColumn> Thin(List<ChartPoint> run, DateTimeOffset from, TimeSpan columnWidth) =>
        run.GroupBy(point => (long)((point.At - from) / columnWidth))
            .Select(column => new ChartColumn(
                from + TimeSpan.FromTicks((long)column.Average(point => (point.At - from).Ticks)),
                column.Average(point => point.Value),
                column.Min(point => point.Value),
                column.Max(point => point.Value)))
            .ToList();
}

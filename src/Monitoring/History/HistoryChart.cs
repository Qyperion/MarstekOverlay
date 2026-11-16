using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.History;

/// <summary>The load on the backup output, the charge and the battery power over a period, ready to be drawn:
/// thinned to the width of the chart and cut into separate runs wherever nothing is known about the time in
/// between.</summary>
public sealed record HistoryChart
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }

    /// <summary>In watts, with what the samples behind every point kept within: averaging hides a short peak,
    /// and the range keeps it.</summary>
    public IReadOnlyList<IReadOnlyList<ChartColumn>> Load { get; init; } = [];

    /// <summary>In percent.</summary>
    public IReadOnlyList<IReadOnlyList<ChartPoint>> Charge { get; init; } = [];

    /// <summary>In watts, positive while charging.</summary>
    public IReadOnlyList<IReadOnlyList<ChartPoint>> Battery { get; init; } = [];

    public IReadOnlyList<ChartSpan> OnBattery { get; init; } = [];

    /// <summary>Of every sample in the period rather than of the thinned points; nothing without samples.</summary>
    public LoadSummary? Summary { get; init; }

    /// <summary>A round value somewhat above the highest load to scale the load to, so that the scale does not
    /// shift with every sample.</summary>
    public Power LoadCeiling { get; init; } = PowerCeiling.Lowest;

    /// <summary>What the charge kept within over the period, for a chart that has no height for the whole scale.</summary>
    public ChargeScale ChargeScale { get; init; } = ChargeScale.Whole;

    /// <summary>Fitted to the points of <see cref="Battery"/> rather than to every sample: the line has no band
    /// to show a peak that averaging has smoothed away, and a scale made for that peak would only press the
    /// line against zero.</summary>
    public BatteryScale BatteryScale { get; init; } = BatteryScale.Narrowest;

    /// <summary>How far from a point of a line its value still stands for a moment asked about: the width of
    /// a column or the longest gap a line is drawn across, whichever is longer.</summary>
    public TimeSpan PointReach { get; init; }

    /// <param name="samples">Oldest first.</param>
    /// <param name="columns">How many points across the chart are worth drawing; samples that fall into one
    /// column are averaged.</param>
    /// <param name="longestBridgedGap">Samples further apart are not joined by a line.</param>
    public static HistoryChart Build(
        IReadOnlyList<StationSample> samples,
        DateTimeOffset from,
        DateTimeOffset to,
        int columns,
        TimeSpan longestBridgedGap)
    {
        if (to <= from || columns <= 0)
            return new HistoryChart { From = from, To = to };

        var shown = samples.Where(sample => sample.At >= from && sample.At <= to).ToList();
        var columnWidth = (to - from) / columns;
        var summary = Summarize(shown);
        var battery = ChartLines.Averages(
            ChartLines.Trace(shown, sample => sample.BatteryPower?.Watts, from, columnWidth, longestBridgedGap));

        return new HistoryChart
        {
            From = from,
            To = to,
            Load = ChartLines.Trace(shown, sample => sample.BackupOutputPower?.Watts, from, columnWidth, longestBridgedGap),
            Charge = ChartLines.Averages(ChartLines.Trace(shown, sample => sample.SocPercent, from, columnWidth, longestBridgedGap)),
            Battery = battery,
            OnBattery = ChartLines.BatterySpans(shown, longestBridgedGap),
            Summary = summary,
            LoadCeiling = summary is { Max.Watts: var highest } ? PowerCeiling.Above(highest) : PowerCeiling.Lowest,
            ChargeScale = ChargeScale.Around(shown.Select(sample => sample.SocPercent).OfType<double>().ToList()),
            BatteryScale = BatteryScale.Around([.. battery.SelectMany(run => run).Select(point => point.Value)]),
            PointReach = columnWidth > longestBridgedGap ? columnWidth : longestBridgedGap,
        };
    }

    /// <summary>The points of the lines nearest to <paramref name="moment"/>, as a pointer over the chart reads
    /// them.</summary>
    /// <returns>Nothing where no line has a point within <see cref="PointReach"/>.</returns>
    public ChartReading? ReadAt(DateTimeOffset moment)
    {
        var load = Nearest(Load, column => column.At, moment);
        var charge = Nearest(Charge, point => point.At, moment);
        var battery = Nearest(Battery, point => point.At, moment);

        if ((load?.AveragePoint ?? charge ?? battery) is not { } point)
            return null;

        return new ChartReading(
            point.At,
            load is { } column ? new Power(column.Average) : null,
            load is { } range ? new Power(range.Max) : null,
            charge?.Value,
            battery is { } flow ? new Power(flow.Value) : null,
            OnBattery.Any(span => span.From <= point.At && point.At <= span.To));
    }

    private static LoadSummary? Summarize(IReadOnlyList<StationSample> samples)
    {
        var loads = samples.Select(sample => sample.BackupOutputPower?.Watts).OfType<double>().ToList();

        return loads.Count > 0
            ? new LoadSummary(new Power(loads.Min()), new Power(loads.Average()), new Power(loads.Max()))
            : null;
    }

    private T? Nearest<T>(IReadOnlyList<IReadOnlyList<T>> runs, Func<T, DateTimeOffset> placeOf, DateTimeOffset moment)
        where T : struct
    {
        T? nearest = null;
        var nearestDistance = TimeSpan.MaxValue;

        foreach (var point in runs.SelectMany(run => run))
        {
            var distance = (placeOf(point) - moment).Duration();
            if (distance <= PointReach && distance < nearestDistance)
            {
                nearest = point;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}

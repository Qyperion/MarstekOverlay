namespace MarstekOverlay.Monitoring.History;

/// <summary>The samples of one line that fell into one column of a chart, drawn as a single point.</summary>
/// <param name="At">The average moment of the samples.</param>
/// <param name="Min">The lowest of the samples: averaging hides a short dip, and this keeps it.</param>
/// <param name="Max">The highest of the samples: averaging hides a short peak, and this keeps it.</param>
public readonly record struct ChartColumn(DateTimeOffset At, double Average, double Min, double Max)
{
    public ChartPoint AveragePoint => new(At, Average);
}

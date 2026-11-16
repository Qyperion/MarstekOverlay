using MarstekOverlay.Monitoring.Outages;

namespace MarstekOverlay.Monitoring.History;

/// <param name="IsDayStart">Falls on a midnight, where the mark is better named by its date than by its time.</param>
public readonly record struct TimeTick(DateTimeOffset At, bool IsDayStart);

/// <summary>Where the marks of a chart's axes go: at round values and round times of day, as many as there is
/// room for.</summary>
public static class ChartAxis
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private static readonly TimeSpan[] TimeSteps =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        Day,
        2 * Day,
        7 * Day,
    ];

    private static readonly double[] ValueSteps = [1, 2, 2.5, 5, 10, 20, 25, 50, 100];

    // A step is taken to divide the scale when the division is off by no more than rounding.
    private const double WholeWithin = 1e-9;

    /// <summary>Marks at round times of day in <paramref name="zone"/>, the same distance apart.</summary>
    /// <param name="atMost">How many marks the axis has room for.</param>
    public static IReadOnlyList<TimeTick> TimeTicks(DateTimeOffset from, DateTimeOffset to, int atMost, TimeZoneInfo zone)
    {
        if (to <= from || atMost <= 0)
            return [];

        var step = StepOver(to - from, atMost);
        var midnight = TimeZoneInfo.ConvertTime(from, zone).Date;

        // Counted from a fixed day rather than from the start of the period, so that marks several days apart
        // stay on the same days while the period moves on.
        var first = step >= Day
            ? new DateTime(midnight.Ticks / step.Ticks * step.Ticks)
            : midnight;

        var ticks = new List<TimeTick>();

        for (var wallTime = first; ; wallTime += step)
        {
            var moments = CalendarPeriods.MomentsAt(wallTime, zone);
            if (moments[0] > to)
                break;

            ticks.AddRange(moments.Select(at => new TimeTick(at, IsDayStart: wallTime.TimeOfDay == TimeSpan.Zero)));
        }

        // A clock set forward names an hour that never comes, at the moment of the mark after it; a clock set
        // back names an hour twice, and both get a mark.
        return [.. ticks.Where(tick => tick.At >= from && tick.At <= to).DistinctBy(tick => tick.At).OrderBy(tick => tick.At)];
    }

    /// <summary>Marks at the multiples of a round step that fall within the scale, zero among them when the scale
    /// holds it; the ends of the scale get one each whenever a round step divides them both.</summary>
    /// <param name="atMost">How many steps the axis has room for from its bottom to its top.</param>
    public static IReadOnlyList<double> ValueTicks(double floor, double ceiling, int atMost)
    {
        if (ceiling <= floor || atMost <= 0)
            return [];

        var span = ceiling - floor;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(span / atMost)));
        var fitting = ValueSteps
            .Select(step => step * magnitude)
            .Where(step => span / step <= atMost + WholeWithin)
            .ToList();
        var chosen = fitting
            .Where(step => IsWhole(floor / step) && IsWhole(ceiling / step))
            .DefaultIfEmpty(fitting[0])
            .First();

        var first = (int)Math.Ceiling(floor / chosen - WholeWithin);
        var last = (int)Math.Floor(ceiling / chosen + WholeWithin);

        return Enumerable.Range(first, last - first + 1)
            .Select(index => index * chosen)
            .ToList();
    }

    private static TimeSpan StepOver(TimeSpan period, int atMost)
    {
        foreach (var step in TimeSteps)
        {
            if (period / step <= atMost)
                return step;
        }

        return Day * Math.Ceiling(period / Day / atMost);
    }

    private static bool IsWhole(double value) => Math.Abs(value - Math.Round(value)) <= WholeWithin;
}

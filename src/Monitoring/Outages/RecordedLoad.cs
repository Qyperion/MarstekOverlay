using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.Monitoring.Outages;

/// <summary>The backup output the samples recorded, by which the energy of an outage that reaches beyond a period
/// is shared out by when it was drawn. Where no sample tells, the rest of the outage's energy goes by time, so
/// with no samples at all the share is the share of the time.</summary>
/// <param name="samples">Oldest first.</param>
/// <param name="longestGap">How far apart two samples may be and still tell about the time between them.</param>
public sealed class RecordedLoad(IReadOnlyList<StationSample> samples, TimeSpan longestGap)
{
    public static readonly RecordedLoad None = new([], TimeSpan.Zero);

    private readonly StationSample[] _samples = [.. samples.Where(sample => sample.BackupOutputPower is not null)];

    /// <returns>How much of the energy of <paramref name="outage"/> was drawn between <paramref name="from"/> and
    /// <paramref name="to"/>, from 0 to 1.</returns>
    public double ShareOf(Outage outage, DateTimeOffset from, DateTimeOffset to)
    {
        var startedAt = Later(outage.StartedAt, from);
        var endedAt = Earlier(outage.EndedAt, to);

        if (endedAt <= startedAt)
            return outage.Duration > TimeSpan.Zero ? 0 : 1;

        if (startedAt == outage.StartedAt && endedAt == outage.EndedAt)
            return 1;

        var whole = Recorded(outage.StartedAt, outage.EndedAt);
        var part = Recorded(startedAt, endedAt);
        var unrecorded = outage.Duration - whole.Covered;

        // What the samples do not account for was drawn while they were missing, evenly as far as is known.
        var drawnUnrecorded = Math.Max(outage.ConsumedEnergy.WattHours - whole.WattHours, 0);
        var wattsUnrecorded = unrecorded > TimeSpan.Zero ? drawnUnrecorded / unrecorded.TotalHours : 0;

        var estimatedWhole = whole.WattHours + wattsUnrecorded * unrecorded.TotalHours;
        var estimatedPart = part.WattHours + wattsUnrecorded * (endedAt - startedAt - part.Covered).TotalHours;

        return estimatedWhole > 0
            ? Math.Clamp(estimatedPart / estimatedWhole, 0, 1)
            : (endedAt - startedAt) / outage.Duration;
    }

    // Between two samples close enough the output is taken to have changed evenly, as the journal takes it.
    private (double WattHours, TimeSpan Covered) Recorded(DateTimeOffset from, DateTimeOffset to)
    {
        var wattHours = 0.0;
        var covered = TimeSpan.Zero;

        for (var index = 1; index < _samples.Length; index++)
        {
            var (before, after) = (_samples[index - 1], _samples[index]);
            var span = after.At - before.At;

            if (span <= TimeSpan.Zero || span > longestGap || after.At <= from || before.At >= to)
                continue;

            var start = Later(before.At, from);
            var end = Earlier(after.At, to);
            var (wattsBefore, wattsAfter) = (Watts(before), Watts(after));
            double WattsAt(DateTimeOffset moment) => wattsBefore + (wattsAfter - wattsBefore) * ((moment - before.At) / span);

            wattHours += (WattsAt(start) + WattsAt(end)) / 2 * (end - start).TotalHours;
            covered += end - start;
        }

        return (wattHours, covered);
    }

    private static double Watts(StationSample sample) => Math.Max(sample.BackupOutputPower?.Watts ?? 0, 0);

    private static DateTimeOffset Later(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;

    private static DateTimeOffset Earlier(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;
}

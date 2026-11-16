namespace MarstekOverlay.Monitoring.History;

/// <summary>The part of the charge scale a chart too low for the whole of it draws: just what the charge passed
/// through over the period, so that the change takes the full height. The ends are round values with some room
/// beyond the readings, and never closer than <see cref="NarrowestSpanPercent"/>: the charge is reported in whole
/// percents, and a steady one would otherwise be drawn as the noise of its rounding.</summary>
public readonly record struct ChargeScale(double FloorPercent, double CeilingPercent)
{
    internal const double NarrowestSpanPercent = 10;

    public static readonly ChargeScale Whole = new(EmptyPercent, Percent.Whole);

    private const double EmptyPercent = 0;
    private const double StepPercent = 5;
    private const double RoomPercent = 1;

    /// <returns>The whole scale when there is nothing to fit it to.</returns>
    public static ChargeScale Around(IReadOnlyCollection<double> socPercents)
    {
        if (socPercents.Count == 0)
            return Whole;

        var floor = Math.Max(Math.Floor((socPercents.Min() - RoomPercent) / StepPercent) * StepPercent, EmptyPercent);
        var ceiling = Math.Min(Math.Ceiling((socPercents.Max() + RoomPercent) / StepPercent) * StepPercent, Percent.Whole);

        if (ceiling - floor >= NarrowestSpanPercent)
            return new ChargeScale(floor, ceiling);

        // A full battery is the usual case, so the scale grows downwards for as long as there is room.
        floor = Math.Max(ceiling - NarrowestSpanPercent, EmptyPercent);

        return new ChargeScale(floor, floor + NarrowestSpanPercent);
    }
}

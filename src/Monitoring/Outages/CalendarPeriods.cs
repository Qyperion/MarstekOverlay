namespace MarstekOverlay.Monitoring.Outages;

/// <summary>Where the day, the week and the month a moment belongs to begin by the clock of a time zone.</summary>
public static class CalendarPeriods
{
    internal const int DaysPerWeek = 7;

    public static DateTimeOffset DayStart(DateTimeOffset moment, TimeZoneInfo zone) =>
        At(TimeZoneInfo.ConvertTime(moment, zone).Date, zone);

    /// <param name="firstDayOfWeek">A regional setting: Monday in most of Europe, Sunday in the United States.</param>
    public static DateTimeOffset WeekStart(DateTimeOffset moment, TimeZoneInfo zone, DayOfWeek firstDayOfWeek)
    {
        var today = TimeZoneInfo.ConvertTime(moment, zone).Date;
        var daysIntoWeek = (today.DayOfWeek - firstDayOfWeek + DaysPerWeek) % DaysPerWeek;

        return At(today.AddDays(-daysIntoWeek), zone);
    }

    public static DateTimeOffset MonthStart(DateTimeOffset moment, TimeZoneInfo zone) =>
        CalendarMonth.Of(moment, zone).StartsAt(zone);

    /// <summary>The first moment the clock of <paramref name="zone"/> shows <paramref name="wallTime"/>.</summary>
    internal static DateTimeOffset At(DateTime wallTime, TimeZoneInfo zone) => MomentsAt(wallTime, zone)[0];

    /// <summary>Every moment the clock of <paramref name="zone"/> shows <paramref name="wallTime"/>, the earliest
    /// first: an hour the clock is set back over comes twice.</summary>
    internal static IReadOnlyList<DateTimeOffset> MomentsAt(DateTime wallTime, TimeZoneInfo zone) =>
        zone.IsAmbiguousTime(wallTime)
            ? [.. zone.GetAmbiguousTimeOffsets(wallTime).Select(offset => new DateTimeOffset(wallTime, offset)).Order()]
            : [new DateTimeOffset(wallTime, zone.GetUtcOffset(wallTime))];
}

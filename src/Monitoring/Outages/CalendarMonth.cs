namespace MarstekOverlay.Monitoring.Outages;

/// <summary>A month of the calendar, the page the outage journal is leafed through by.</summary>
public readonly record struct CalendarMonth(int Year, int Month) : IComparable<CalendarMonth>
{
    public CalendarMonth Previous => Shifted(-1);

    public CalendarMonth Next => Shifted(1);

    public static bool operator <(CalendarMonth left, CalendarMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(CalendarMonth left, CalendarMonth right) => left.CompareTo(right) > 0;

    /// <summary>The month <paramref name="moment"/> falls into by the clock of <paramref name="zone"/>.</summary>
    public static CalendarMonth Of(DateTimeOffset moment, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(moment, zone);

        return new CalendarMonth(local.Year, local.Month);
    }

    public DateTimeOffset StartsAt(TimeZoneInfo zone) => CalendarPeriods.At(new DateTime(Year, Month, 1), zone);

    /// <summary>The first moment of the month that follows.</summary>
    public DateTimeOffset EndsAt(TimeZoneInfo zone) => Next.StartsAt(zone);

    public int CompareTo(CalendarMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    private CalendarMonth Shifted(int months)
    {
        var first = new DateTime(Year, Month, 1).AddMonths(months);

        return new CalendarMonth(first.Year, first.Month);
    }
}

namespace MarstekOverlay.DesktopTests.TestSupport;

internal static class Moments
{
    /// <summary>A moment of November 2026 by the clock on the wall, so that the texts made of it read the same in
    /// whatever time zone the tests run. Europe changes no clock in that month; North America goes back from summer
    /// time at 02:00 on the 1st, so no test may reach across that hour.</summary>
    public static DateTimeOffset Local(int day, int hour, int minute, int second = 0) => LocalIn(11, day, hour, minute, second);

    /// <summary>A moment of another month of 2026, for a journal leafed through by month. Of October, only the
    /// days before the 25th are clear of the change from summer time.</summary>
    public static DateTimeOffset LocalIn(int month, int day, int hour, int minute, int second = 0, int year = 2026) =>
        new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local));
}

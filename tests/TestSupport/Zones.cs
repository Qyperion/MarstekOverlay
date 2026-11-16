namespace MarstekOverlay.TestSupport;

/// <summary>Time zones made by the tests themselves, so that no expectation depends on the zone data of the machine.</summary>
public static class Zones
{
    /// <summary>The offset of <see cref="Fixed"/>, and the winter offset of <see cref="Kyiv"/>.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    /// <summary>A zone that never changes its clock.</summary>
    public static readonly TimeZoneInfo Fixed = TimeZoneInfo.CreateCustomTimeZone("Fixed", Offset, "Fixed", "Fixed");

    /// <summary>The rules of Kyiv since 1996: an hour ahead from the last Sunday of March, 03:00, to the last Sunday of
    /// October, 04:00.</summary>
    public static readonly TimeZoneInfo Kyiv = TimeZoneInfo.CreateCustomTimeZone(
        "Kyiv",
        Offset,
        "Kyiv",
        "Kyiv winter",
        "Kyiv summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(1996, 1, 1),
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                LastSunday(month: 3, hour: 3),
                LastSunday(month: 10, hour: 4)),
        ]);

    private static TimeZoneInfo.TransitionTime LastSunday(int month, int hour) =>
        TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, hour, 0, 0), month, week: 5, DayOfWeek.Sunday);
}

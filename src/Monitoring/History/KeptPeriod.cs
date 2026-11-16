using MarstekOverlay.Monitoring.Outages;

namespace MarstekOverlay.Monitoring.History;

/// <summary>How much of what was recorded a clean-up leaves, counted back from now.</summary>
public enum KeptPeriod
{
    Nothing,
    Day,
    Week,
    Month,
}

public static class KeptPeriodExtensions
{
    // A month here is a length of time, not a page of the calendar.
    private const int DaysPerMonth = 30;

    extension(KeptPeriod period)
    {
        /// <returns>The moment the period begins at: whatever was recorded before it is not kept.</returns>
        public DateTimeOffset StartsAt(DateTimeOffset now) => period switch
        {
            KeptPeriod.Day => now - TimeSpan.FromDays(1),
            KeptPeriod.Week => now - TimeSpan.FromDays(CalendarPeriods.DaysPerWeek),
            KeptPeriod.Month => now - TimeSpan.FromDays(DaysPerMonth),
            _ => DateTimeOffset.MaxValue,
        };
    }
}

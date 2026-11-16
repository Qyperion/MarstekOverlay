using FluentAssertions;

using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class CalendarPeriodsTests
{
    // Thursday, the 12th of November 2026.
    private static readonly DateTimeOffset Now = Local(month: 11, day: 12, hour: 12, minute: 42);

    [Fact]
    public void DayStart_IsTheMidnightBefore()
    {
        CalendarPeriods.DayStart(Now, Zones.Fixed).Should().Be(Local(month: 11, day: 12));
    }

    [Fact]
    public void DayStart_MomentToldByAnotherClock_GoesByTheClockOfTheZone()
    {
        // Arrange
        var lateEveningInUtc = new DateTimeOffset(2026, 11, 12, 22, 30, 0, TimeSpan.Zero);

        // Act
        var start = CalendarPeriods.DayStart(lateEveningInUtc, Zones.Fixed);

        // Assert
        start.Should().Be(Local(month: 11, day: 13));
    }

    [Fact]
    public void DayStart_AtMidnightItself_IsThatMidnight()
    {
        // Arrange
        var midnight = Local(month: 11, day: 12);

        // Act & Assert
        CalendarPeriods.DayStart(midnight, Zones.Fixed).Should().Be(midnight);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 11, 9)]
    [InlineData(DayOfWeek.Sunday, 11, 8)]
    [InlineData(DayOfWeek.Saturday, 11, 7)]
    [InlineData(DayOfWeek.Thursday, 11, 12)]
    [InlineData(DayOfWeek.Friday, 11, 6)]
    public void WeekStart_IsTheMidnightTheWeekBeganAtByTheRegionalFirstDay(DayOfWeek firstDayOfWeek, int month, int day)
    {
        CalendarPeriods.WeekStart(Now, Zones.Fixed, firstDayOfWeek).Should().Be(Local(month, day));
    }

    [Fact]
    public void WeekStart_EarlyInAMonth_MayLieInTheMonthBefore()
    {
        // Arrange
        var firstOfDecember = Local(month: 12, day: 1, hour: 8);

        // Act
        var start = CalendarPeriods.WeekStart(firstOfDecember, Zones.Fixed, DayOfWeek.Monday);

        // Assert
        start.Should().Be(Local(month: 11, day: 30));
        start.Should().BeBefore(CalendarPeriods.MonthStart(firstOfDecember, Zones.Fixed));
    }

    [Fact]
    public void MonthStart_IsTheMidnightOfTheFirst()
    {
        CalendarPeriods.MonthStart(Now, Zones.Fixed).Should().Be(Local(month: 11, day: 1));
    }

    [Fact]
    public void DayStart_ZoneWithSummerTime_TakesTheOffsetInForceThatDay()
    {
        // Arrange
        var kyiv = Zones.Kyiv;
        var summerNoon = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(3));

        // Act
        var start = CalendarPeriods.DayStart(summerNoon, kyiv);

        // Assert
        start.Should().Be(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.FromHours(3)));
        start.Offset.Should().Be(TimeSpan.FromHours(3));
    }

    [Fact]
    public void DayStart_ClockSetBackOverMidnight_IsTheFirstMidnight()
    {
        // Arrange
        var summer = TimeSpan.FromHours(3);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 29),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 25));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Back at one", Zones.Offset, "Back at one", "Winter", "Summer", [rule]);
        var noon = new DateTimeOffset(2026, 10, 25, 12, 0, 0, Zones.Offset);

        // Act
        var start = CalendarPeriods.DayStart(noon, zone);

        // Assert
        start.Should().Be(new DateTimeOffset(2026, 10, 25, 0, 0, 0, summer));
    }

    private static DateTimeOffset Local(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, Zones.Offset);
}

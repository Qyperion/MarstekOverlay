using FluentAssertions;

using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class CalendarMonthTests
{
    [Fact]
    public void Of_MomentInsideAMonth_IsThatMonth()
    {
        // Arrange
        var moment = new DateTimeOffset(2026, 11, 12, 12, 42, 0, Zones.Offset);

        // Act & Assert
        CalendarMonth.Of(moment, Zones.Fixed).Should().Be(new CalendarMonth(2026, 11));
    }

    [Fact]
    public void Of_LastEveningOfAMonthByAnotherClock_GoesByTheClockOfTheZone()
    {
        // Arrange
        var newYearInTheZone = new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero);

        // Act & Assert
        CalendarMonth.Of(newYearInTheZone, Zones.Fixed).Should().Be(new CalendarMonth(2027, 1));
    }

    [Fact]
    public void StartsAtAndEndsAt_SpanTheMonthFromMidnightToMidnight()
    {
        // Arrange
        var february = new CalendarMonth(2028, 2);

        // Act & Assert
        february.StartsAt(Zones.Fixed).Should().Be(new DateTimeOffset(2028, 2, 1, 0, 0, 0, Zones.Offset));
        february.EndsAt(Zones.Fixed).Should().Be(new DateTimeOffset(2028, 3, 1, 0, 0, 0, Zones.Offset));
        (february.EndsAt(Zones.Fixed) - february.StartsAt(Zones.Fixed)).Should().Be(TimeSpan.FromDays(29));
    }

    [Fact]
    public void PreviousAndNext_AcrossTheTurnOfAYear_ChangeTheYear()
    {
        new CalendarMonth(2027, 1).Previous.Should().Be(new CalendarMonth(2026, 12));
        new CalendarMonth(2026, 12).Next.Should().Be(new CalendarMonth(2027, 1));
        new CalendarMonth(2026, 6).Previous.Next.Should().Be(new CalendarMonth(2026, 6));
    }

    [Fact]
    public void Comparison_GoesByTheYearFirst()
    {
        // Arrange
        var december = new CalendarMonth(2026, 12);
        var january = new CalendarMonth(2027, 1);

        // Act & Assert
        (december < january).Should().BeTrue();
        (january > december).Should().BeTrue();
        new[] { january, december }.Order().Should().Equal(december, january);
    }
}

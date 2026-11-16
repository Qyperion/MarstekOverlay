using FluentAssertions;

using MarstekOverlay.Monitoring.History;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class ChartAxisTests
{
    private static readonly TimeSpan Winter = TimeSpan.FromHours(2);
    private static readonly TimeSpan Summer = TimeSpan.FromHours(3);

    [Fact]
    public void TimeTicks_PeriodOfAnHour_PutsMarksAtRoundMinutes()
    {
        // Arrange
        var from = Local(12, 14, 7);
        var to = Local(12, 15, 7);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 6, Zones.Fixed);

        // Assert
        ticks.Should().Equal(
            new TimeTick(Local(12, 14, 10), false),
            new TimeTick(Local(12, 14, 20), false),
            new TimeTick(Local(12, 14, 30), false),
            new TimeTick(Local(12, 14, 40), false),
            new TimeTick(Local(12, 14, 50), false),
            new TimeTick(Local(12, 15, 0), false));
    }

    [Fact]
    public void TimeTicks_LessRoomForMarks_SpreadsThemFurtherApart()
    {
        // Arrange
        var from = Local(12, 14, 7);
        var to = Local(12, 15, 7);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 3, Zones.Fixed);

        // Assert
        ticks.Should().Equal(
            new TimeTick(Local(12, 14, 30), false),
            new TimeTick(Local(12, 15, 0), false));
    }

    [Fact]
    public void TimeTicks_PeriodAcrossMidnight_MarksTheStartOfTheDayInTheZone()
    {
        // Arrange
        var from = Local(11, 15, 40);
        var to = Local(12, 15, 40);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 4, Zones.Fixed);

        // Assert
        ticks.Should().Equal(
            new TimeTick(Local(11, 18, 0), false),
            new TimeTick(Local(12, 0, 0), true),
            new TimeTick(Local(12, 6, 0), false),
            new TimeTick(Local(12, 12, 0), false));
    }

    [Fact]
    public void TimeTicks_PeriodOfAWeek_PutsAMarkAtEveryMidnight()
    {
        // Arrange
        var from = Local(5, 15, 40);
        var to = Local(12, 15, 40);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 8, Zones.Fixed);

        // Assert
        ticks.Should().Equal(Enumerable.Range(6, 7).Select(day => new TimeTick(Local(day, 0, 0), true)));
    }

    [Fact]
    public void TimeTicks_MarksSeveralDaysApart_StayOnTheSameDaysAsThePeriodMovesOn()
    {
        // Arrange
        var from = Local(5, 15, 40);
        var to = Local(12, 15, 40);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 4, Zones.Fixed);
        var aDayLater = ChartAxis.TimeTicks(from + TimeSpan.FromDays(1), to + TimeSpan.FromDays(1), atMost: 4, Zones.Fixed);

        // Assert
        ticks.Should().HaveCountGreaterThanOrEqualTo(3).And.OnlyContain(tick => tick.IsDayStart);
        (ticks[1].At - ticks[0].At).Should().Be(TimeSpan.FromDays(2));
        aDayLater.Should().Contain(ticks.Skip(1));
    }

    [Fact]
    public void TimeTicks_NoPeriodOrNoRoom_GivesNoMarks()
    {
        // Arrange
        var moment = Local(12, 12, 0);

        // Act & Assert
        ChartAxis.TimeTicks(moment, moment, atMost: 5, Zones.Fixed).Should().BeEmpty();
        ChartAxis.TimeTicks(moment, moment + TimeSpan.FromHours(1), atMost: 0, Zones.Fixed).Should().BeEmpty();
    }

    [Fact]
    public void TimeTicks_ClockSetBack_MarksTheRepeatedHourTwice()
    {
        // Arrange
        var kyiv = Zones.Kyiv;
        var from = new DateTimeOffset(2026, 10, 25, 1, 30, 0, Summer);
        var to = new DateTimeOffset(2026, 10, 25, 5, 30, 0, Winter);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 6, kyiv);

        // Assert
        ticks.Select(tick => tick.At).Should().Equal(
            new DateTimeOffset(2026, 10, 25, 2, 0, 0, Summer),
            new DateTimeOffset(2026, 10, 25, 3, 0, 0, Summer),
            new DateTimeOffset(2026, 10, 25, 3, 0, 0, Winter),
            new DateTimeOffset(2026, 10, 25, 4, 0, 0, Winter),
            new DateTimeOffset(2026, 10, 25, 5, 0, 0, Winter));
    }

    [Fact]
    public void TimeTicks_ClockSetForward_MarksTheHourThatCameOnce()
    {
        // Arrange
        var kyiv = Zones.Kyiv;
        var from = new DateTimeOffset(2026, 3, 29, 1, 30, 0, Winter);
        var to = new DateTimeOffset(2026, 3, 29, 5, 30, 0, Summer);

        // Act
        var ticks = ChartAxis.TimeTicks(from, to, atMost: 3, kyiv);

        // Assert
        ticks.Select(tick => tick.At).Should().Equal(
            new DateTimeOffset(2026, 3, 29, 2, 0, 0, Winter),
            new DateTimeOffset(2026, 3, 29, 4, 0, 0, Summer),
            new DateTimeOffset(2026, 3, 29, 5, 0, 0, Summer));
    }

    [Theory]
    [InlineData(100, 4, new double[] { 0, 25, 50, 75, 100 })]
    [InlineData(100, 2, new double[] { 0, 50, 100 })]
    [InlineData(150, 4, new double[] { 0, 50, 100, 150 })]
    [InlineData(300, 4, new double[] { 0, 100, 200, 300 })]
    [InlineData(500, 4, new double[] { 0, 250, 500 })]
    [InlineData(500, 5, new double[] { 0, 100, 200, 300, 400, 500 })]
    [InlineData(600, 4, new double[] { 0, 200, 400, 600 })]
    [InlineData(800, 4, new double[] { 0, 200, 400, 600, 800 })]
    [InlineData(1000, 4, new double[] { 0, 250, 500, 750, 1000 })]
    [InlineData(3000, 3, new double[] { 0, 1000, 2000, 3000 })]
    public void ValueTicks_RoundCeiling_EndsWithAMarkAtTheCeiling(double ceiling, int atMost, double[] expected)
    {
        // Act
        var ticks = ChartAxis.ValueTicks(0, ceiling, atMost);

        // Assert
        ticks.Should().Equal(expected);
    }

    [Fact]
    public void ValueTicks_CeilingNoRoundStepDivides_StopsAtTheLastMarkBelowIt()
    {
        // Act
        var ticks = ChartAxis.ValueTicks(floor: 0, ceiling: 70, atMost: 2);

        // Assert
        ticks.Should().Equal(0, 50);
    }

    [Fact]
    public void ValueTicks_NothingToMeasure_GivesNoMarks()
    {
        // Act & Assert
        ChartAxis.ValueTicks(floor: 0, ceiling: 0, atMost: 4).Should().BeEmpty();
        ChartAxis.ValueTicks(floor: 0, ceiling: 100, atMost: 0).Should().BeEmpty();
        ChartAxis.ValueTicks(floor: 50, ceiling: 50, atMost: 4).Should().BeEmpty();
        ChartAxis.ValueTicks(floor: -100, ceiling: 100, atMost: 0).Should().BeEmpty();
    }

    [Theory]
    [InlineData(-100, 100, 2, new double[] { -100, 0, 100 })]
    [InlineData(-100, 100, 1, new double[] { 0 })]
    [InlineData(-1000, 3000, 4, new double[] { -1000, 0, 1000, 2000, 3000 })]
    [InlineData(-400, 100, 2, new double[] { -250, 0 })]
    [InlineData(-400, 100, 5, new double[] { -400, -300, -200, -100, 0, 100 })]
    // A step of 200 would fit and end at the ceiling, yet miss the floor: the one that marks both ends is taken.
    [InlineData(-250, 1000, 7, new double[] { -250, 0, 250, 500, 750, 1000 })]
    // The discharge side is too short for a mark of its own at this step: zero still gets one.
    [InlineData(-400, 3000, 2, new double[] { 0, 2000 })]
    [InlineData(-400, 3000, 4, new double[] { 0, 1000, 2000, 3000 })]
    public void ValueTicks_ScaleOnBothSidesOfZero_PutsMarksAtMultiplesOfOneStepWithZeroAmongThem(
        double floor,
        double ceiling,
        int atMost,
        double[] expected)
    {
        // Act
        var ticks = ChartAxis.ValueTicks(floor, ceiling, atMost);

        // Assert
        ticks.Should().Equal(expected);
    }

    private static DateTimeOffset Local(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, Zones.Offset);
}

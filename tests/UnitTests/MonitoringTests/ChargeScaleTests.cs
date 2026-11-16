using FluentAssertions;

using MarstekOverlay.Monitoring.History;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class ChargeScaleTests
{
    [Fact]
    public void Around_NoReadings_IsTheWholeScale()
    {
        ChargeScale.Around([]).Should().Be(ChargeScale.Whole).And.Be(new ChargeScale(0, 100));
    }

    [Theory]
    // An outage: the scale ends at round values with room beyond the readings.
    [InlineData(62, 100, 60, 100)]
    [InlineData(61, 97, 60, 100)]
    [InlineData(60, 94, 55, 95)]
    [InlineData(23, 48, 20, 50)]
    public void Around_ChargeThatMoved_SpansWhatItPassedThrough(double lowest, double highest, double floor, double ceiling)
    {
        ChargeScale.Around([highest, lowest, (lowest + highest) / 2]).Should().Be(new ChargeScale(floor, ceiling));
    }

    [Theory]
    // A full battery with its top-ups must not be drawn as swings over the whole height.
    [InlineData(100, 100, 90, 100)]
    [InlineData(99, 100, 90, 100)]
    [InlineData(50, 50, 45, 55)]
    [InlineData(52, 52, 45, 55)]
    [InlineData(3, 4, 0, 10)]
    [InlineData(0, 0, 0, 10)]
    public void Around_SteadyCharge_KeepsTheNarrowestSpan(double lowest, double highest, double floor, double ceiling)
    {
        // Act
        var scale = ChargeScale.Around([lowest, highest]);

        // Assert
        scale.Should().Be(new ChargeScale(floor, ceiling));
        (scale.CeilingPercent - scale.FloorPercent).Should().Be(ChargeScale.NarrowestSpanPercent);
    }

    [Theory]
    [InlineData(-3, 20, 0, 25)]
    [InlineData(80, 104, 75, 100)]
    [InlineData(101, 104, 90, 100)]
    public void Around_ReadingsOffTheScale_StayWithinIt(double lowest, double highest, double floor, double ceiling)
    {
        ChargeScale.Around([lowest, highest]).Should().Be(new ChargeScale(floor, ceiling));
    }
}

using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class BatteryScaleTests
{
    [Fact]
    public void Around_NoReadings_IsTheNarrowestScale()
    {
        BatteryScale.Around([]).Should().Be(BatteryScale.Narrowest).And.Be(Scale(-100, 100));
    }

    [Fact]
    public void Around_BatteryAtRest_KeepsTheNarrowestScale()
    {
        BatteryScale.Around([0, 0, 3, -8]).Should().Be(Scale(-100, 100));
    }

    [Theory]
    // An outage alone: the discharge takes the height, the charging side stays at its least.
    [InlineData(-320, 0, -400, 100)]
    // Recharging after it: each side reaches a round value somewhat beyond what the power came to.
    [InlineData(-320, 2250, -400, 3000)]
    [InlineData(-1200, 2470, -1500, 3000)]
    // Charging alone.
    [InlineData(0, 2250, -100, 3000)]
    [InlineData(40, 800, -100, 1000)]
    public void Around_PowerThatWentEitherWay_ReachesAsFarAsItWentOnEachSide(
        double lowestWatts,
        double highestWatts,
        double floorWatts,
        double ceilingWatts)
    {
        BatteryScale.Around([highestWatts, lowestWatts]).Should().Be(Scale(floorWatts, ceilingWatts));
    }

    private static BatteryScale Scale(double floorWatts, double ceilingWatts) => new(new Power(floorWatts), new Power(ceilingWatts));
}

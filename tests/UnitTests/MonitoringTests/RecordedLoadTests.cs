using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class RecordedLoadTests
{
    private const double SharePrecision = 0.000_1;

    private static readonly DateTimeOffset StartedAt = new(2026, 10, 31, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Bound = StartedAt + TimeSpan.FromHours(1);
    private static readonly DateTimeOffset EndedAt = StartedAt + TimeSpan.FromHours(2);
    private static readonly TimeSpan LongestGap = TimeSpan.FromHours(1);

    [Fact]
    public void ShareOf_NoSamples_SharesByTime()
    {
        // Arrange
        var outage = Outage(wattHours: 400);

        // Act
        var share = RecordedLoad.None.ShareOf(outage, Bound - TimeSpan.FromMinutes(30), EndedAt);

        // Assert
        share.Should().BeApproximately(0.75, SharePrecision);
    }

    [Fact]
    public void ShareOf_SamplesThroughTheWholeOutage_SharesByTheLoadTheyRecorded()
    {
        // Arrange
        var load = new RecordedLoad([Sample(0, 100), Sample(30, 100), Sample(60, 100), Sample(90, 300), Sample(120, 300)], LongestGap);

        // Act
        var share = load.ShareOf(Outage(wattHours: 350), Bound, EndedAt);

        // Assert
        share.Should().BeApproximately(250.0 / 350, SharePrecision);
    }

    [Fact]
    public void ShareOf_SamplesThroughPartOfTheOutage_SharesTheRestOfItsEnergyByTime()
    {
        // Arrange
        var load = new RecordedLoad([Sample(0, 100), Sample(30, 100), Sample(60, 100)], LongestGap);
        var outage = Outage(wattHours: 400);

        // Act
        var before = load.ShareOf(outage, StartedAt, Bound);
        var after = load.ShareOf(outage, Bound, EndedAt);

        // Assert
        before.Should().BeApproximately(0.25, SharePrecision);
        after.Should().BeApproximately(0.75, SharePrecision);
    }

    [Fact]
    public void ShareOf_SamplesTooFarApart_TellNothingAboutTheTimeBetweenThem()
    {
        // Arrange
        var load = new RecordedLoad([Sample(0, 1000), Sample(120, 1000)], LongestGap);

        // Act
        var share = load.ShareOf(Outage(wattHours: 400), Bound, EndedAt);

        // Assert
        share.Should().BeApproximately(0.5, SharePrecision);
    }

    [Fact]
    public void ShareOf_PeriodHoldingTheWholeOutage_TakesAllOfIt()
    {
        // Arrange
        var load = new RecordedLoad([Sample(0, 100), Sample(60, 900)], LongestGap);

        // Act
        var share = load.ShareOf(Outage(wattHours: 400), StartedAt - TimeSpan.FromDays(1), EndedAt + TimeSpan.FromDays(1));

        // Assert
        share.Should().Be(1);
    }

    private static Outage Outage(double wattHours) => OutageRecords.Finished(StartedAt, EndedAt - StartedAt, wattHours);

    private static StationSample Sample(int minutesIn, double loadWatts) =>
        new(StartedAt + TimeSpan.FromMinutes(minutesIn), 80, new Power(-loadWatts), new Power(loadWatts), PowerSource.Battery);
}

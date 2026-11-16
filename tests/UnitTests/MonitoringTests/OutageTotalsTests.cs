using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class OutageTotalsTests
{
    private static readonly DateTimeOffset To = new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset From = To - TimeSpan.FromDays(1);

    private const double WattHourPrecision = 0.001;

    [Fact]
    public void Over_NoOutages_AddsUpToNothing()
    {
        // Act
        var totals = OutageTotals.Over([], From, To, RecordedLoad.None);

        // Assert
        totals.Should().Be(OutageTotals.None);
    }

    [Fact]
    public void Over_OutagesInsideThePeriod_AddsUpTheirTimeAndEnergy()
    {
        // Arrange
        Outage[] outages =
        [
            Finished(hoursBeforeEnd: 20, hours: 2, wattHours: 500),
            Finished(hoursBeforeEnd: 5, hours: 0.5, wattHours: 120),
        ];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Count.Should().Be(2);
        totals.Duration.Should().Be(TimeSpan.FromHours(2.5));
        totals.ConsumedEnergy.WattHours.Should().BeApproximately(620, WattHourPrecision);
        totals.IsDurationExact.Should().BeTrue();
    }

    [Fact]
    public void Over_OutageThatBeganBeforeThePeriod_CountsThePartInsideIt()
    {
        // Arrange
        Outage[] outages = [Finished(hoursBeforeEnd: 25, hours: 4, wattHours: 800)];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Count.Should().Be(1);
        totals.Duration.Should().Be(TimeSpan.FromHours(3));
        totals.ConsumedEnergy.WattHours.Should().BeApproximately(600, WattHourPrecision);
    }

    [Fact]
    public void Over_OutageThatBeganBeforeThePeriodWithItsLoadRecorded_CountsTheEnergyDrawnInside()
    {
        // Arrange
        var outage = Finished(hoursBeforeEnd: 25, hours: 2, wattHours: 400);
        StationSample[] samples =
        [
            Sample(outage.StartedAt, loadWatts: 300),
            Sample(From, loadWatts: 300),
            Sample(From + TimeSpan.FromMinutes(1), loadWatts: 100),
            Sample(outage.EndedAt, loadWatts: 100),
        ];

        // Act
        var totals = OutageTotals.Over([outage], From, To, new RecordedLoad(samples, TimeSpan.FromHours(1)));

        // Assert
        var recordedBefore = 300.0;
        var recordedInside = 200.0 / 60 + 100.0 * 59 / 60;
        totals.Duration.Should().Be(TimeSpan.FromHours(1));
        totals.ConsumedEnergy.WattHours.Should().BeApproximately(400 * recordedInside / (recordedBefore + recordedInside), WattHourPrecision);
    }

    [Fact]
    public void Over_OutageWhoseUnseenStartLiesBeforeThePeriod_KnowsThePartInsideExactly()
    {
        // Arrange
        Outage[] outages = [Finished(hoursBeforeEnd: 25, hours: 4, wattHours: 800) with { IsStartObserved = false }];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Duration.Should().Be(TimeSpan.FromHours(3));
        totals.IsDurationExact.Should().BeTrue();
    }

    [Fact]
    public void Over_OutageOutsideThePeriod_LeavesItOut()
    {
        // Arrange
        Outage[] outages =
        [
            Finished(hoursBeforeEnd: 30, hours: 2, wattHours: 500),
            Finished(hoursBeforeEnd: -1, hours: 1, wattHours: 250),
        ];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Should().Be(OutageTotals.None);
    }

    [Fact]
    public void Over_OngoingOutage_CountsItUpToWhenItWasLastSeen()
    {
        // Arrange
        Outage[] outages = [Finished(hoursBeforeEnd: 1, hours: 1, wattHours: 250) with { IsOngoing = true, IsEndObserved = false }];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Count.Should().Be(1);
        totals.Duration.Should().Be(TimeSpan.FromHours(1));
        totals.IsDurationExact.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Over_OutageSeenOnlyInPart_TellsThatItLastedAtLeastThatLong(bool isStartObserved, bool isEndObserved)
    {
        // Arrange
        Outage[] outages =
        [
            Finished(hoursBeforeEnd: 20, hours: 2, wattHours: 500),
            Finished(hoursBeforeEnd: 5, hours: 1, wattHours: 250) with { IsStartObserved = isStartObserved, IsEndObserved = isEndObserved },
        ];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Duration.Should().Be(TimeSpan.FromHours(3));
        totals.IsDurationExact.Should().BeFalse();
    }

    [Fact]
    public void Over_OutageWithoutLength_CountsItWithAllOfItsEnergy()
    {
        // Arrange
        Outage[] outages = [Finished(hoursBeforeEnd: 3, hours: 0, wattHours: 2)];

        // Act
        var totals = OutageTotals.Over(outages, From, To, RecordedLoad.None);

        // Assert
        totals.Count.Should().Be(1);
        totals.Duration.Should().Be(TimeSpan.Zero);
        totals.ConsumedEnergy.Should().Be(new Energy(2));
    }

    private static StationSample Sample(DateTimeOffset at, double loadWatts) =>
        new(at, 80, new Power(-loadWatts), new Power(loadWatts), PowerSource.Battery);

    private static Outage Finished(double hoursBeforeEnd, double hours, double wattHours) =>
        OutageRecords.Finished(To - TimeSpan.FromHours(hoursBeforeEnd), TimeSpan.FromHours(hours), wattHours);
}

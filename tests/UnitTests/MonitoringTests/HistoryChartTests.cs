using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Supply;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class HistoryChartTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From + TimeSpan.FromHours(1);
    private static readonly TimeSpan BridgedGap = TimeSpan.FromMinutes(2);

    private const int Columns = 240;

    [Fact]
    public void Build_SamplesOutsideThePeriod_LeavesThemOut()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: -10, loadWatts: 900),
            Sample(minutes: 10, loadWatts: 250),
            Sample(minutes: 70, loadWatts: 900),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        LoadLine(chart).Should().ContainSingle().Which.Should().Equal(new ChartPoint(At(10), 250));
        chart.Summary.Should().Be(new LoadSummary(new Power(250), new Power(250), new Power(250)));
    }

    [Fact]
    public void Build_FewerSamplesThanColumns_KeepsEverySampleWhereItIs()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 1, loadWatts: 200, soc: 91),
            Sample(minutes: 1.5, loadWatts: 260, soc: 90),
            Sample(minutes: 2, loadWatts: 230, soc: 90),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        LoadLine(chart).Should().ContainSingle().Which.Should().Equal(
            new ChartPoint(At(1), 200),
            new ChartPoint(At(1.5), 260),
            new ChartPoint(At(2), 230));
        chart.Charge.Should().ContainSingle().Which.Should().Equal(
            new ChartPoint(At(1), 91),
            new ChartPoint(At(1.5), 90),
            new ChartPoint(At(2), 90));
    }

    [Fact]
    public void Build_MoreSamplesThanColumns_AveragesEachColumn()
    {
        // Arrange
        var samples = Enumerable.Range(0, 60).Select(minute => Sample(minute, loadWatts: 100 + minute)).ToList();

        // Act
        var chart = Build(samples, columns: 6, bridgedGap: TimeSpan.FromMinutes(2));

        // Assert
        LoadLine(chart).Should().ContainSingle().Which.Should().Equal(
            new ChartPoint(At(4.5), 104.5),
            new ChartPoint(At(14.5), 114.5),
            new ChartPoint(At(24.5), 124.5),
            new ChartPoint(At(34.5), 134.5),
            new ChartPoint(At(44.5), 144.5),
            new ChartPoint(At(54.5), 154.5));
    }

    [Fact]
    public void Build_MoreSamplesThanColumns_KeepsWhatEachColumnKeptWithin()
    {
        // Arrange
        var samples = Enumerable.Range(0, 60).Select(minute => Sample(minute, loadWatts: 100 + minute)).ToList();

        // Act
        var chart = Build(samples, columns: 6, bridgedGap: TimeSpan.FromMinutes(2));

        // Assert
        chart.Load.Should().ContainSingle().Which.Should().Equal(
            new ChartColumn(At(4.5), 104.5, 100, 109),
            new ChartColumn(At(14.5), 114.5, 110, 119),
            new ChartColumn(At(24.5), 124.5, 120, 129),
            new ChartColumn(At(34.5), 134.5, 130, 139),
            new ChartColumn(At(44.5), 144.5, 140, 149),
            new ChartColumn(At(54.5), 154.5, 150, 159));
    }

    [Fact]
    public void Build_ShortPeakInsideAColumn_ShowsInTheRangeThoughTheAverageHidesIt()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, loadWatts: 100),
            Sample(minutes: 0.5, loadWatts: 700),
            Sample(minutes: 1, loadWatts: 100),
        ];

        // Act
        var chart = Build(samples, columns: 1, bridgedGap: BridgedGap);

        // Assert
        LoadLine(chart).Should().ContainSingle().Which.Should().Equal(new ChartPoint(At(0.5), 300));
        chart.Load.Should().ContainSingle().Which.Should().Equal(new ChartColumn(At(0.5), 300, 100, 700));
    }

    [Fact]
    public void Build_FewerSamplesThanColumns_HasRangesNoWiderThanTheSamples()
    {
        // Arrange
        StationSample[] samples = [Sample(minutes: 1, loadWatts: 200), Sample(minutes: 1.5, loadWatts: 260)];

        // Act
        var chart = Build(samples);

        // Assert
        chart.Load.Should().ContainSingle().Which.Should().Equal(
            new ChartColumn(At(1), 200, 200, 200),
            new ChartColumn(At(1.5), 260, 260, 260));
    }

    [Fact]
    public void Build_SamplesFurtherApartThanBridged_LeavesAGapBetweenThem()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, loadWatts: 200),
            Sample(minutes: 0.5, loadWatts: 210),
            Sample(minutes: 2.5, loadWatts: 220),
            Sample(minutes: 10, loadWatts: 300),
            Sample(minutes: 10.5, loadWatts: 310),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        LoadLine(chart).Should().HaveCount(2);
        LoadLine(chart)[0].Select(point => point.Value).Should().Equal(200, 210, 220);
        LoadLine(chart)[1].Select(point => point.Value).Should().Equal(300, 310);
        chart.Charge.Should().HaveCount(2);
    }

    [Fact]
    public void Build_GapNarrowerThanAColumn_StillBreaksTheLine()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 1, loadWatts: 200),
            Sample(minutes: 4, loadWatts: 400),
        ];

        // Act
        var chart = Build(samples, columns: 6, bridgedGap: BridgedGap);

        // Assert
        LoadLine(chart).Should().HaveCount(2);
        LoadLine(chart)[0].Should().Equal(new ChartPoint(At(1), 200));
        LoadLine(chart)[1].Should().Equal(new ChartPoint(At(4), 400));
    }

    [Fact]
    public void Build_SampleWithoutALoad_IsLeftOutOfThatLineOnly()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, loadWatts: 200),
            Sample(minutes: 0.5, loadWatts: null),
            Sample(minutes: 1, loadWatts: 220),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        LoadLine(chart).Should().ContainSingle().Which.Should().HaveCount(2);
        chart.Charge.Should().ContainSingle().Which.Should().HaveCount(3);
        chart.Summary.Should().Be(new LoadSummary(new Power(200), new Power(210), new Power(220)));
    }

    [Fact]
    public void Build_OnBattery_SpansFromTheFirstSampleOnItToTheOneThatShowsTheMains()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, source: PowerSource.Mains),
            Sample(minutes: 0.5, source: PowerSource.Battery),
            Sample(minutes: 1, source: PowerSource.Battery),
            Sample(minutes: 1.5, source: PowerSource.Mains),
            Sample(minutes: 2, source: PowerSource.Mains),
            Sample(minutes: 2.5, source: PowerSource.Battery),
            Sample(minutes: 3, source: PowerSource.Unknown),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        chart.OnBattery.Should().Equal(new ChartSpan(At(0.5), At(1.5)), new ChartSpan(At(2.5), At(3)));
    }

    [Fact]
    public void Build_AnswersStopWhileOnBattery_EndsTheSpanWhereItWasLastSeen()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, source: PowerSource.Battery),
            Sample(minutes: 0.5, source: PowerSource.Battery),
            Sample(minutes: 20, source: PowerSource.Mains),
            Sample(minutes: 30, source: PowerSource.Battery),
            Sample(minutes: 40, source: PowerSource.Battery),
            Sample(minutes: 40.5, source: PowerSource.Battery),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        chart.OnBattery.Should().Equal(
            new ChartSpan(At(0), At(0.5)),
            new ChartSpan(At(30), At(30)),
            new ChartSpan(At(40), At(40.5)));
    }

    [Fact]
    public void Build_Summary_CountsEverySampleNotTheThinnedPoints()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, loadWatts: 100),
            Sample(minutes: 0.5, loadWatts: 700),
            Sample(minutes: 1, loadWatts: 100),
        ];

        // Act
        var chart = Build(samples, columns: 1, bridgedGap: BridgedGap);

        // Assert
        LoadLine(chart).Should().ContainSingle().Which.Should().Equal(new ChartPoint(At(0.5), 300));
        chart.Summary.Should().Be(new LoadSummary(new Power(100), new Power(300), new Power(700)));
        chart.LoadCeiling.Should().Be(new Power(1000));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(40, 100)]
    [InlineData(100, 150)]
    [InlineData(290, 400)]
    [InlineData(330, 400)]
    [InlineData(560, 800)]
    [InlineData(1000, 1500)]
    [InlineData(2470, 3000)]
    public void Build_LoadCeiling_IsARoundValueSomewhatAboveTheHighestLoad(double highestWatts, double expectedWatts)
    {
        // Arrange
        StationSample[] samples = [Sample(minutes: 0, loadWatts: 20), Sample(minutes: 0.5, loadWatts: highestWatts)];

        // Act
        var chart = Build(samples);

        // Assert
        chart.LoadCeiling.Should().Be(new Power(expectedWatts));
    }

    [Fact]
    public void Build_ChargeThatFell_ScalesTheChargeToWhatItPassedThroughInThePeriod()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: -10, soc: 20),
            Sample(minutes: 10, soc: 100, source: PowerSource.Battery),
            Sample(minutes: 10.5, soc: 82, source: PowerSource.Battery),
            Sample(minutes: 11, soc: 63, source: PowerSource.Battery),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        chart.ChargeScale.Should().Be(new ChargeScale(60, 100));
    }

    [Fact]
    public void Build_BatteryPower_IsALineOfItsOwnThatGoesBothWays()
    {
        // Arrange
        StationSample[] samples =
        [
            Sample(minutes: 0, batteryWatts: 0),
            Sample(minutes: 0.5, batteryWatts: -320, source: PowerSource.Battery),
            Sample(minutes: 1, batteryWatts: -300, source: PowerSource.Battery),
            Sample(minutes: 1.5, batteryWatts: 2250),
            Sample(minutes: 10, batteryWatts: null),
            Sample(minutes: 20, batteryWatts: 40),
        ];

        // Act
        var chart = Build(samples);

        // Assert
        chart.Battery.Should().HaveCount(2);
        chart.Battery[0].Should().Equal(
            new ChartPoint(At(0), 0),
            new ChartPoint(At(0.5), -320),
            new ChartPoint(At(1), -300),
            new ChartPoint(At(1.5), 2250));
        chart.Battery[1].Should().Equal(new ChartPoint(At(20), 40));
        chart.BatteryScale.Should().Be(new BatteryScale(new Power(-400), new Power(3000)));
    }

    [Fact]
    public void Build_BatteryPowerMoreSamplesThanColumns_AveragesEachColumn()
    {
        // Arrange
        var samples = Enumerable.Range(0, 60).Select(minute => Sample(minute, batteryWatts: minute < 30 ? -200 : 2000)).ToList();

        // Act
        var chart = Build(samples, columns: 2, bridgedGap: TimeSpan.FromMinutes(2));

        // Assert
        chart.Battery.Should().ContainSingle().Which.Should().Equal(
            new ChartPoint(At(14.5), -200),
            new ChartPoint(At(44.5), 2000));
    }

    [Fact]
    public void Build_BatteryPeakThatAveragingSmoothsAway_FitsTheScaleToTheLineRatherThanToThePeak()
    {
        // Arrange
        var samples = Enumerable.Range(0, 60).Select(minute => Sample(minute, batteryWatts: minute == 22 ? -2000 : -200)).ToList();

        // Act
        var thinned = Build(samples, columns: 6, bridgedGap: TimeSpan.FromMinutes(2));
        var whole = Build(samples, columns: 3600, bridgedGap: TimeSpan.FromMinutes(2));

        // Assert
        thinned.Battery.Should().ContainSingle().Which.Min(point => point.Value).Should().Be(-380);
        thinned.BatteryScale.Should().Be(new BatteryScale(new Power(-500), new Power(100)));
        whole.BatteryScale.Should().Be(new BatteryScale(new Power(-3000), new Power(100)));
    }

    [Fact]
    public void Build_NoSamples_HasNothingToDraw()
    {
        // Act
        var chart = Build([]);

        // Assert
        chart.From.Should().Be(From);
        chart.To.Should().Be(To);
        LoadLine(chart).Should().BeEmpty();
        chart.Charge.Should().BeEmpty();
        chart.OnBattery.Should().BeEmpty();
        chart.Summary.Should().BeNull();
        chart.LoadCeiling.Should().Be(new Power(100));
    }

    [Fact]
    public void Build_NoSamples_KeepsTheWholeChargeScale()
    {
        Build([]).ChargeScale.Should().Be(ChargeScale.Whole);
    }

    [Fact]
    public void Build_NoSamples_KeepsTheNarrowestBatteryScale()
    {
        // Act
        var chart = Build([]);

        // Assert
        chart.Load.Should().BeEmpty();
        chart.Battery.Should().BeEmpty();
        chart.BatteryScale.Should().Be(BatteryScale.Narrowest);
    }

    [Fact]
    public void Build_NothingToDrawOn_HasNothingToDraw()
    {
        // Arrange
        StationSample[] samples = [Sample(minutes: 0)];

        // Act
        var withoutColumns = Build(samples, columns: 0, bridgedGap: BridgedGap);
        var withoutPeriod = HistoryChart.Build(samples, From, From, Columns, BridgedGap);

        // Assert
        LoadLine(withoutColumns).Should().BeEmpty();
        LoadLine(withoutPeriod).Should().BeEmpty();
    }

    [Fact]
    public void ReadAt_MomentBetweenTwoSamples_ReadsTheNearerOne()
    {
        // Arrange
        var chart = Build(
        [
            Sample(minutes: 10, loadWatts: 200, soc: 91),
            Sample(minutes: 10.5, loadWatts: 260, soc: 90),
        ]);

        // Act
        var reading = chart.ReadAt(At(10.4));

        // Assert
        reading.Should().Be(new ChartReading(At(10.5), new Power(260), new Power(260), 90, Power.Zero, IsOnBattery: false));
    }

    [Fact]
    public void ReadAt_MomentFarFromAnySample_ReadsNothing()
    {
        // Arrange
        var chart = Build([Sample(minutes: 10), Sample(minutes: 30)]);

        // Act & Assert
        chart.ReadAt(At(11.5)).Should().NotBeNull();
        chart.ReadAt(At(20)).Should().BeNull();
        chart.ReadAt(At(27.5)).Should().BeNull();
    }

    [Fact]
    public void ReadAt_ColumnsWiderThanTheBridgedGap_ReachesAsFarAsAColumn()
    {
        // Arrange
        var samples = Enumerable.Range(0, 60).Select(minute => Sample(minute, loadWatts: 100 + minute)).ToList();
        var chart = Build(samples, columns: 6, bridgedGap: TimeSpan.FromMinutes(2));

        // Act
        var reading = chart.ReadAt(At(20));

        // Assert
        reading.Should().NotBeNull();
        reading.Value.At.Should().Be(At(24.5));
        reading.Value.Load.Should().Be(new Power(124.5));
    }

    [Fact]
    public void ReadAt_MomentOnTheBattery_TellsSo()
    {
        // Arrange
        var chart = Build(
        [
            Sample(minutes: 10),
            Sample(minutes: 10.5, source: PowerSource.Battery),
            Sample(minutes: 11, source: PowerSource.Battery),
            Sample(minutes: 11.5),
            Sample(minutes: 12),
        ]);

        // Act & Assert
        chart.ReadAt(At(10)).Should().BeEquivalentTo(new { IsOnBattery = false });
        chart.ReadAt(At(11)).Should().BeEquivalentTo(new { IsOnBattery = true });
        chart.ReadAt(At(12)).Should().BeEquivalentTo(new { IsOnBattery = false });
    }

    [Fact]
    public void ReadAt_SampleWithoutALoad_ReadsTheChargeAlone()
    {
        // Arrange
        var chart = Build([Sample(minutes: 10, loadWatts: null, soc: 77)]);

        // Act
        var reading = chart.ReadAt(At(10));

        // Assert
        reading.Should().Be(new ChartReading(At(10), Load: null, LoadMax: null, 77, Power.Zero, IsOnBattery: false));
    }

    [Fact]
    public void ReadAt_PointThatStandsForSeveralSamples_TellsTheHighestOfThem()
    {
        // Arrange
        var samples = Enumerable.Range(0, 60)
            .Select(minute => Sample(minute, loadWatts: minute == 22 ? 900 : 100, batteryWatts: -110))
            .ToList();
        var chart = Build(samples, columns: 6, bridgedGap: TimeSpan.FromMinutes(2));

        // Act
        var reading = chart.ReadAt(At(25));

        // Assert
        reading.Should().Be(new ChartReading(At(24.5), new Power(180), new Power(900), 100, new Power(-110), IsOnBattery: false));
    }

    [Fact]
    public void ReadAt_SampleWithBatteryPowerAlone_ReadsThatAlone()
    {
        // Arrange
        var sample = new StationSample(At(10), SocPercent: null, new Power(-250), BackupOutputPower: null, PowerSource.Battery);
        var chart = Build([sample]);

        // Act
        var reading = chart.ReadAt(At(10));

        // Assert
        reading.Should().Be(new ChartReading(At(10), Load: null, LoadMax: null, SocPercent: null, new Power(-250), IsOnBattery: true));
    }

    private static HistoryChart Build(IReadOnlyList<StationSample> samples) => Build(samples, Columns, BridgedGap);

    private static HistoryChart Build(IReadOnlyList<StationSample> samples, int columns, TimeSpan bridgedGap) =>
        HistoryChart.Build(samples, From, To, columns, bridgedGap);

    private static IReadOnlyList<IReadOnlyList<ChartPoint>> LoadLine(HistoryChart chart) =>
        [.. chart.Load.Select(run => (IReadOnlyList<ChartPoint>)[.. run.Select(column => column.AveragePoint)])];

    private static DateTimeOffset At(double minutes) => From + TimeSpan.FromMinutes(minutes);

    private static StationSample Sample(
        double minutes,
        double? loadWatts = 250,
        double soc = 100,
        PowerSource source = PowerSource.Mains,
        double? batteryWatts = 0) => new(
            At(minutes),
            soc,
            batteryWatts is { } battery ? new Power(battery) : null,
            loadWatts is { } watts ? new Power(watts) : null,
            source);
}

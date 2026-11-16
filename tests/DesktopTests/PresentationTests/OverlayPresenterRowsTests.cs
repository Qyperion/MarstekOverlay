using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;


public class OverlayPresenterRowsTests : OverlayPresenterTests
{
    [Fact]
    public void Present_RowsNobodyChose_AreThoseShownByDefault()
    {
        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt).With(Supplies.OnBattery(YesterdayEvening)));

        // Assert
        state.Rows.Select(row => row.Kind).Should().Equal(
            OverlayRowKind.Forecast,
            OverlayRowKind.Battery,
            OverlayRowKind.Grid,
            OverlayRowKind.BackupOutput,
            OverlayRowKind.Solar);
        state.Rows.Where(row => row.IsProminent).Select(row => row.Kind)
            .Should().Equal(OverlayRowKind.Forecast, OverlayRowKind.Battery);
    }

    [Fact]
    public void Present_Rows_NameTheirReadingsApartFromTheValues()
    {
        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt).With(Supplies.OnBattery(YesterdayEvening)));

        // Assert
        state.Rows.Select(row => row.Label).Should().Equal(null, "Battery", "Grid", "Backup output", "Solar");
    }

    [Fact]
    public void Present_RowsChosenInTheSettings_KeepTheirOrder()
    {
        // Arrange
        Overlay.Show(OverlayRowKind.Grid, isShown: false);
        Overlay.Show(OverlayRowKind.Solar, isShown: false);
        Overlay.Show(OverlayRowKind.WifiSignal, isShown: true);
        Overlay.Show(OverlayRowKind.StoredEnergy, isShown: true);

        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt).With(Supplies.OnBattery(YesterdayEvening)));

        // Assert
        state.Rows.Select(row => row.Kind).Should().Equal(
            OverlayRowKind.Forecast,
            OverlayRowKind.Battery,
            OverlayRowKind.StoredEnergy,
            OverlayRowKind.BackupOutput,
            OverlayRowKind.WifiSignal);
    }

    [Fact]
    public void Present_FullBatteryOnMains_DoesNotTakeTheLoadForTheBatterys()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt, loadWatts: 250);

        // Act
        var onMains = RowOf(OverlayRowKind.Battery, snapshot.With(Supplies.OnMains(YesterdayEvening)));
        var sourceUnknown = RowOf(OverlayRowKind.Battery, snapshot.With());

        // Assert
        onMains.Value.Should().Be("0 W");
        sourceUnknown.Value.Should().Be("-250 W");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OnBattery_ShowsTheWorkedOutBatteryPowerWithoutAMark(string language)
    {
        // Arrange
        Speak(language);
        var state = StationReadings.OnBattery(AnsweredAt, loadWatts: 286).With(Supplies.OnBattery(YesterdayEvening));

        // Act
        var row = RowOf(OverlayRowKind.Battery, state);

        // Assert
        row.Value.Should().Be(Translated("-286 W"));
    }

    [Fact]
    public void Present_BatteryPowerReportedByTheStation_IsShownAsItIs()
    {
        // Arrange
        var status = new EnergySystemStatus { BatterySocPercent = 80, BatteryPower = new Power(180) };

        // Act
        var row = RowOf(OverlayRowKind.Battery, StationReadings.Answered(AnsweredAt, status).With());

        // Assert
        row.Value.Should().Be("180 W");
    }

    [Fact]
    public void Present_PowerFlows_AreShownWithTheirSign()
    {
        // Arrange
        var state = StationReadings.Charging(AnsweredAt, loadWatts: 250).With(Supplies.OnMains(YesterdayEvening));

        // Act
        var grid = RowOf(OverlayRowKind.Grid, state);
        var backupOutput = RowOf(OverlayRowKind.BackupOutput, state);
        var solar = RowOf(OverlayRowKind.Solar, state);

        // Assert
        grid.Value.Should().Be("-2450 W");
        backupOutput.Value.Should().Be("250 W");
        solar.Value.Should().Be("0 W");
    }

    [Fact]
    public void Present_ReadingsOfTheSlowPoll_FillTheirRows()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt) with
        {
            Battery = new Reading<BatteryStatus>(new BatteryStatus { TemperatureCelsius = 22 }, AnsweredAt),
            Wifi = new Reading<WifiStatus>(new WifiStatus { SignalStrengthDbm = -62 }, AnsweredAt),
            Mode = new Reading<EnergySystemMode>(new EnergySystemMode { Mode = OperatingMode.Ups }, AnsweredAt),
        };
        var state = snapshot.With(forecast: new RuntimeForecast(ForecastStatus.Collecting, StoredEnergy: new Energy(4096)));

        // Act
        var storedEnergy = RowOf(OverlayRowKind.StoredEnergy, state);
        var temperature = RowOf(OverlayRowKind.BatteryTemperature, state);
        var wifi = RowOf(OverlayRowKind.WifiSignal, state);
        var mode = RowOf(OverlayRowKind.OperatingMode, state);

        // Assert
        storedEnergy.Value.Should().Be("4.1 kWh");
        temperature.Value.Should().Be("22 °C");
        wifi.Value.Should().Be("-62 dBm");
        mode.Value.Should().Be("UPS");
    }

    [Fact]
    public void Present_AnswerToAnotherRequestGoneOld_SaysNoDataRatherThanPassItForThePresent()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt) with
        {
            Battery = new Reading<BatteryStatus>(new BatteryStatus { TemperatureCelsius = 22 }, Now - TimeSpan.FromMinutes(17)),
            Wifi = new Reading<WifiStatus>(new WifiStatus { SignalStrengthDbm = -62 }, Now - TimeSpan.FromMinutes(16)),
            Mode = new Reading<EnergySystemMode>(new EnergySystemMode { Mode = OperatingMode.Ups }, Now - TimeSpan.FromMinutes(3)),
        };
        var state = snapshot.With();

        // Act
        var temperature = RowOf(OverlayRowKind.BatteryTemperature, state);
        var wifi = RowOf(OverlayRowKind.WifiSignal, state);
        var mode = RowOf(OverlayRowKind.OperatingMode, state);

        // Assert
        temperature.Value.Should().Be("no data");
        wifi.Value.Should().Be("-62 dBm", "three slow polls are trusted, each up to a fast interval later than five minutes");
        mode.Value.Should().Be("no data");
    }

    [Fact]
    public void Present_RequestWithoutAnAnswer_SaysNoData()
    {
        // Arrange
        var state = StationSnapshot.Empty.With();

        // Act
        var battery = RowOf(OverlayRowKind.Battery, state);
        var storedEnergy = RowOf(OverlayRowKind.StoredEnergy, state);
        var grid = RowOf(OverlayRowKind.Grid, state);
        var temperature = RowOf(OverlayRowKind.BatteryTemperature, state);
        var outage = RowOf(OverlayRowKind.CurrentOutage, state);

        // Assert
        battery.Value.Should().Be("no data");
        storedEnergy.Value.Should().Be("no data");
        grid.Value.Should().Be("no data");
        temperature.Value.Should().Be("no data");
        outage.Value.Should().Be("no data");
    }

    [Fact]
    public void Present_AnswerWithoutTheValue_SaysUnavailable()
    {
        // Arrange
        var snapshot = StationReadings.Answered(AnsweredAt, new EnergySystemStatus { BatterySocPercent = 80 }) with
        {
            Battery = new Reading<BatteryStatus>(new BatteryStatus(), AnsweredAt),
        };
        var state = snapshot.With();

        // Act
        var battery = RowOf(OverlayRowKind.Battery, state);
        var storedEnergy = RowOf(OverlayRowKind.StoredEnergy, state);
        var temperature = RowOf(OverlayRowKind.BatteryTemperature, state);

        // Assert
        battery.Value.Should().Be("unavailable");
        storedEnergy.Value.Should().Be("unavailable");
        temperature.Value.Should().Be("unavailable");
    }
}

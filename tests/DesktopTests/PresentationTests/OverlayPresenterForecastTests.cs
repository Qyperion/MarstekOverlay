using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;


public class OverlayPresenterForecastTests : OverlayPresenterTests
{
    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_Discharging_ForecastsWhenTheReserveIsReached(string language)
    {
        // Arrange
        Speak(language);
        var forecast = new RuntimeForecast(ForecastStatus.TimeToReserve, EndsAt: Now + new TimeSpan(9, 2, 0));

        // Act
        var row = ForecastOnBattery(StationReadings.OnBattery(AnsweredAt), forecast);

        // Assert
        row.Value.Should().Be(Translated("≈ 9 h 00 min · until 07:40 tomorrow"));
        row.IsProminent.Should().BeTrue();
        row.Hint.Should().Be(Translated(
            "Time until the charge drops to the reserve of 12%, at the load averaged over the last 5 minutes and with 90% of what the battery gives up reaching the outputs. Not a guarantee."));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_ChargedBySolarDuringAnOutage_ForecastsWhenTheBatteryIsFull(string language)
    {
        // Arrange
        Speak(language);
        var status = new EnergySystemStatus
        {
            BatterySocPercent = 80,
            PvPower = new Power(800),
            OngridPower = Power.Zero,
            OffgridPower = new Power(250),
        };
        var forecast = new RuntimeForecast(ForecastStatus.TimeToFull, EndsAt: Now + TimeSpan.FromMinutes(20));

        // Act
        var row = ForecastOnBattery(StationReadings.Answered(AnsweredAt, status), forecast);

        // Assert
        row.Value.Should().Be(Translated("Full in ≈ 0 h 20 min · by 23:00"));
        row.Hint.Should().Contain("97%", "charging has an allowance for losses of its own").And.NotContain("90%");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_ForecastEndsLaterThanTomorrow_NamesTheDayOfTheWeek(string language)
    {
        // Arrange
        Speak(language);
        var forecast = new RuntimeForecast(ForecastStatus.TimeToReserve, EndsAt: Now + new TimeSpan(days: 2, hours: 9, minutes: 0, seconds: 0));

        // Act
        var row = ForecastOnBattery(StationReadings.OnBattery(AnsweredAt), forecast);

        // Assert
        row.Value.Should().Be(Translated("≈ 57 h 00 min · until Sun 07:40"));
    }

    [Fact]
    public void Present_RegionalFormatOfAnotherLanguage_StillNamesTheDayInTheLanguageOfTheApp()
    {
        // Arrange
        Speak(English, regionalFormat: RegionalFormatOfUkraine);
        var forecast = new RuntimeForecast(ForecastStatus.TimeToReserve, EndsAt: Now + new TimeSpan(days: 2, hours: 9, minutes: 0, seconds: 0));

        // Act
        var row = ForecastOnBattery(StationReadings.OnBattery(AnsweredAt), forecast);

        // Assert
        row.Value.Should().Be("≈ 57 h 00 min · until Sun 07:40");
    }

    [Theory]
    [InlineData(ForecastStatus.Standby)]
    [InlineData(ForecastStatus.TimeToFull)]
    [InlineData(ForecastStatus.Collecting)]
    public void Present_OnMains_LeavesTheForecastOffThePanel(ForecastStatus status)
    {
        // Arrange
        var forecast = new RuntimeForecast(status, EndsAt: Now + TimeSpan.FromMinutes(20), StandbyRuntime: new TimeSpan(14, 32, 0));

        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With(Supplies.OnMains(YesterdayEvening), forecast));

        // Assert
        state.Rows.Should().NotContain(row => row.Kind == OverlayRowKind.Forecast);
    }

    [Fact]
    public void Present_SourceNotKnownYet_LeavesTheForecastOffThePanel()
    {
        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt).With(forecast: new RuntimeForecast(ForecastStatus.Collecting)));

        // Assert
        state.Rows.Should().NotContain(row => row.Kind == OverlayRowKind.Forecast);
    }

    [Theory]
    [InlineData(4, "Less than 10 min to 12%")]
    [InlineData(5, "≈ 0 h 10 min · until 22:50")]
    [InlineData(7 * 24 * 60, "≈ 168 h 00 min · until Thu 22:40")]
    [InlineData(7 * 24 * 60 + 1, "More than 7 days to 12%")]
    public void Present_ForecastAtTheEdgesOfItsRange_SaysHowFarRatherThanWhen(int minutesLeft, string expected)
    {
        // Arrange
        var forecast = new RuntimeForecast(ForecastStatus.TimeToReserve, EndsAt: Now + TimeSpan.FromMinutes(minutesLeft));

        // Act
        var row = ForecastOnBattery(StationReadings.OnBattery(AnsweredAt), forecast);

        // Assert
        row.Value.Should().Be(expected);
    }

    [Fact]
    public void Present_ChargeAlmostFull_NamesTheFullChargeAsTheTarget()
    {
        // Arrange
        var forecast = new RuntimeForecast(ForecastStatus.TimeToFull, EndsAt: Now + TimeSpan.FromMinutes(2));

        // Act
        var state = Present(StationReadings.Charging(AnsweredAt).With(Supplies.OnMains(YesterdayEvening), forecast));

        // Assert
        state.Summary[^1].Should().Be("Less than 10 min to 100%");
    }

    [Theory]
    [InlineData(ForecastStatus.InsufficientData, "Forecast: not enough data")]
    [InlineData(ForecastStatus.Collecting, "Forecast: collecting data")]
    [InlineData(ForecastStatus.LoadTooLow, "Forecast: load too low")]
    [InlineData(ForecastStatus.ReserveReached, "Reserve of 12% reached")]
    public void Present_NoEstimate_SaysWhy(ForecastStatus status, string expected)
    {
        // Act
        var row = ForecastOnBattery(StationReadings.OnBattery(AnsweredAt), new RuntimeForecast(status));

        // Assert
        row.Value.Should().Be(expected);
    }

    private OverlayRow ForecastOnBattery(StationSnapshot snapshot, RuntimeForecast forecast) =>
        RowOf(OverlayRowKind.Forecast, snapshot.With(Supplies.OnBattery(YesterdayEvening), forecast));
}

using FluentAssertions;

using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;


public class OverlayPresenterCompactTests : OverlayPresenterTests
{
    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_Compact_PutsTheLoadAndTheForecastOnOneLine(string language)
    {
        // Arrange
        Speak(language);
        var forecast = new RuntimeForecast(ForecastStatus.Standby, StandbyRuntime: new TimeSpan(7, 10, 0));

        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt, loadWatts: 575).With(Supplies.OnMains(YesterdayEvening), forecast));

        // Assert
        state.CompactText.Should().Be(Translated("575 W · ≈ 7 h 10 min"));
        state.SocText.Should().Be("100%");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_CompactWhileCharging_SaysWhatTheTimeIsFor(string language)
    {
        // Arrange
        Speak(language);
        var forecast = new RuntimeForecast(ForecastStatus.TimeToFull, EndsAt: Now + TimeSpan.FromMinutes(20));

        // Act
        var state = Present(StationReadings.Charging(AnsweredAt, loadWatts: 575).With(Supplies.OnMains(YesterdayEvening), forecast));

        // Assert
        state.CompactText.Should().Be(Translated("575 W · full in ≈ 0 h 20 min"));
    }

    [Theory]
    [InlineData(ForecastStatus.Collecting, 0, "575 W")]
    [InlineData(ForecastStatus.TimeToReserve, 4, "575 W · < 10 min")]
    [InlineData(ForecastStatus.TimeToReserve, 8 * 24 * 60, "575 W · > 7 days")]
    public void Present_CompactWithoutATimeToTell_KeepsToWhatFits(ForecastStatus status, int minutesLeft, string expected)
    {
        // Arrange
        var forecast = new RuntimeForecast(status, EndsAt: Now + TimeSpan.FromMinutes(minutesLeft));

        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt, loadWatts: 575).With(forecast: forecast));

        // Assert
        state.CompactText.Should().Be(expected);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_Summary_LeadsWithTheSourceAndTheCharge(string language)
    {
        // Arrange
        Speak(language);
        var forecast = new RuntimeForecast(ForecastStatus.Standby, StandbyRuntime: new TimeSpan(14, 30, 0));

        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt, loadWatts: 575).With(Supplies.OnMains(YesterdayEvening), forecast));

        // Assert
        state.Summary.Should().Equal(
            Translated("On mains · 100%"),
            Translated("Backup output: 575 W"),
            Translated("Would last ≈ 14 h 30 min"));
    }
}

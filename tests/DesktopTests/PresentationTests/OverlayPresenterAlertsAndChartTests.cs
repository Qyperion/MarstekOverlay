using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;


public class OverlayPresenterAlertsAndChartTests : OverlayPresenterTests
{
    [Fact]
    public void Present_Alerts_KeepsThoseThePanelHasNoOtherPlaceFor()
    {
        // Arrange
        Alert[] alerts =
        [
            new FlowForbiddenAlert(BatteryFlow.Discharging),
            new LowChargeAlert(ThresholdPercent: 30, SocPercent: 28, IsLowestThreshold: false),
            new StaleDataAlert(AnsweredAt),
        ];

        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt, soc: 28).With(alerts: alerts));

        // Assert
        var forbidden = new OverlayAlert("The station forbids discharging", Tone.Critical);
        var lowCharge = new OverlayAlert("Low charge (threshold 30%)", Tone.Warning);
        state.Alerts.Should().Equal(forbidden, lowCharge);
        state.CompactAlert.Should().Be(new OverlayAlert(forbidden.Text + Environment.NewLine + lowCharge.Text, Tone.Critical));
    }

    [Fact]
    public void Present_OnlyWarnings_CombineIntoAWarning()
    {
        // Arrange
        var lowCharge = new LowChargeAlert(ThresholdPercent: 30, SocPercent: 28, IsLowestThreshold: false);

        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt, soc: 28).With(alerts: lowCharge));

        // Assert
        state.CompactAlert.Should().Be(new OverlayAlert("Low charge (threshold 30%)", Tone.Warning));
    }

    [Fact]
    public void Present_NothingWrong_HasNoAlerts()
    {
        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With());

        // Assert
        state.Alerts.Should().BeEmpty();
        state.CompactAlert.Should().BeNull();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_Chart_ComesWithTheFiguresItHasNoRoomFor(string language)
    {
        // Arrange
        Speak(language);
        var chart = Chart(200, 250, 300);

        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With(), chart);

        // Assert
        state.Chart.Should().NotBeNull();
        state.Chart.Chart.Should().BeSameAs(chart);
        state.Chart.Hint.Should().Be(
            Translated("Load on the backup output over 3 h: min 200 W · average 250 W · max 300 W.")
            + Environment.NewLine
            + Translated(
                "The upper band is the load, 400 W at its full height; the lower one is the charge, from 90% to 100%. Time on the battery is shaded."));
    }

    [Fact]
    public void Present_ChartWithoutSamples_SaysThereAreNone()
    {
        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With(), Chart());

        // Assert
        (state.Chart?.Hint).Should().Be("No readings over 3 h yet.");
    }

    [Fact]
    public void Present_ChartSwitchedOff_LeavesItOut()
    {
        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With());

        // Assert
        state.Chart.Should().BeNull();
    }
}

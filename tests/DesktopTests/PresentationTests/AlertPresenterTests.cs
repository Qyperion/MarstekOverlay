using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class AlertPresenterTests : LocalizedTests
{
    private static readonly DateTimeOffset Now = Moments.Local(day: 12, hour: 12, minute: 42);

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeOnPanel_LowCharge_NamesTheThreshold(string language)
    {
        // Arrange
        Speak(language);
        var alert = new LowChargeAlert(ThresholdPercent: 30, SocPercent: 28, IsLowestThreshold: false);

        // Act
        var warning = AlertPresenter.DescribeOnPanel(alert);

        // Assert
        warning.Should().Be(new OverlayAlert(Translated("Low charge (threshold 30%)"), Tone.Warning));
    }

    [Fact]
    public void DescribeOnPanel_ChargeAtTheLowestThreshold_IsCritical()
    {
        // Arrange
        var alert = new LowChargeAlert(ThresholdPercent: 15, SocPercent: 15, IsLowestThreshold: true);

        // Act
        var warning = AlertPresenter.DescribeOnPanel(alert);

        // Assert
        warning.Should().Be(new OverlayAlert("Critically low charge (threshold 15%)", Tone.Critical));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeOnPanel_BatteryTooHot_TellsTheTemperatureAndTheLimit(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var warning = AlertPresenter.DescribeOnPanel(new BatteryTemperatureAlert(Celsius: 47.5, LimitCelsius: 45));

        // Assert
        warning.Should().Be(new OverlayAlert(Translated("Battery too hot: 47.5 °C (limit 45 °C)"), Tone.Warning));
    }

    [Fact]
    public void DescribeOnPanel_BatteryTooCold_SaysCold()
    {
        // Act
        var warning = AlertPresenter.DescribeOnPanel(new BatteryTemperatureAlert(Celsius: 3, LimitCelsius: 5));

        // Assert
        (warning?.Text).Should().Be("Battery too cold: 3 °C (limit 5 °C)");
    }

    [Theory]
    [InlineData(BatteryFlow.Discharging, "The station forbids discharging", Tone.Critical)]
    [InlineData(BatteryFlow.Charging, "The station forbids charging", Tone.Warning)]
    public void DescribeOnPanel_FlowForbidden_SaysWhichOne(BatteryFlow flow, string text, Tone tone)
    {
        // Act
        var warning = AlertPresenter.DescribeOnPanel(new FlowForbiddenAlert(flow));

        // Assert
        warning.Should().Be(new OverlayAlert(text, tone));
    }

    [Fact]
    public void DescribeOnPanel_WhatTheHeadingAlreadyTells_GetsNoRow()
    {
        // Arrange
        Alert[] toldByTheHeading =
        [
            new MainsLostAlert(SocPercent: 89),
            new MainsRestoredAlert(Outage: null, SocPercent: 89),
            new StaleDataAlert(Now),
        ];

        // Act
        var warnings = toldByTheHeading.Select(AlertPresenter.DescribeOnPanel);

        // Assert
        warnings.Should().AllSatisfy(warning => warning.Should().BeNull());
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Notify_MainsLost_TellsTheChargeTheBatteryStartsWith(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var notification = AlertPresenter.Notify(new MainsLostAlert(SocPercent: 89), Now);

        // Assert
        notification.Should().Be(new Notification(
            Translated("Mains power lost"),
            Translated("The station is running on the battery. Charge: 89%."),
            Tone.Warning));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Notify_StationFoundOnBatteryUnwatched_DoesNotClaimThatTheMainsHaveJustGone(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var notification = AlertPresenter.Notify(new MainsLostAlert(SocPercent: 62, IsSwitchSeen: false), Now);

        // Assert
        notification.Should().Be(new Notification(
            Translated("The station is running on the battery"),
            Translated("The outage began while the app was not following the station. Charge: 62%."),
            Tone.Warning));
    }

    [Fact]
    public void Notify_MainsLostWithoutACharge_LeavesTheChargeOut()
    {
        // Act
        var notification = AlertPresenter.Notify(new MainsLostAlert(SocPercent: null), Now);

        // Assert
        notification.Text.Should().Be("The station is running on the battery.");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Notify_MainsRestored_SumsUpTheOutage(string language)
    {
        // Arrange
        Speak(language);
        var outage = OutageRecords.Finished(Now - TimeSpan.FromMinutes(135), TimeSpan.FromMinutes(135));

        // Act
        var notification = AlertPresenter.Notify(new MainsRestoredAlert(outage, SocPercent: 91), Now);

        // Assert
        notification.Should().Be(new Notification(
            Translated("Mains power is back"),
            Translated("The outage lasted 2 h 15 min and took 1.56 kWh. Charge: 91%."),
            Tone.Info));
    }

    [Fact]
    public void Notify_MainsRestoredAfterAnOutageSeenInPart_SaysAtLeast()
    {
        // Arrange
        var outage = OutageRecords.Finished(Now - TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(40)) with { IsStartObserved = false };

        // Act
        var notification = AlertPresenter.Notify(new MainsRestoredAlert(outage, SocPercent: null), Now);

        // Assert
        notification.Text.Should().Be("The outage lasted at least 40 min and took 1.56 kWh.");
    }

    [Fact]
    public void Notify_MainsRestoredWithoutAnOutageOnRecord_SaysOnlyThatTheyAreBack()
    {
        // Act
        var notification = AlertPresenter.Notify(new MainsRestoredAlert(Outage: null, SocPercent: 91), Now);

        // Assert
        notification.Text.Should().Be("The station is on the mains again. Charge: 91%.");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Notify_LowCharge_TellsTheChargeAndTheThreshold(string language)
    {
        // Arrange
        Speak(language);
        var alert = new LowChargeAlert(ThresholdPercent: 30, SocPercent: 29, IsLowestThreshold: false);

        // Act
        var notification = AlertPresenter.Notify(alert, Now);

        // Assert
        notification.Should().Be(new Notification(
            Translated("Battery charge is low"),
            Translated("The charge is down to 29%; the threshold is 30%."),
            Tone.Warning));
    }

    [Fact]
    public void Notify_ChargeAtTheLowestThreshold_IsCritical()
    {
        // Arrange
        var alert = new LowChargeAlert(ThresholdPercent: 15, SocPercent: 15, IsLowestThreshold: true);

        // Act
        var notification = AlertPresenter.Notify(alert, Now);

        // Assert
        notification.Title.Should().Be("Battery charge is critically low");
        notification.Tone.Should().Be(Tone.Critical);
    }

    [Theory]
    [InlineData(47.5, 45, "The battery is at 47.5 °C, above the limit of 45 °C.")]
    [InlineData(3, 5, "The battery is at 3 °C, below the limit of 5 °C.")]
    public void Notify_BatteryTemperature_SaysWhichLimitIsCrossed(double celsius, double limit, string text)
    {
        // Act
        var notification = AlertPresenter.Notify(new BatteryTemperatureAlert(celsius, limit), Now);

        // Assert
        notification.Should().Be(new Notification("Battery temperature out of range", text, Tone.Warning));
    }

    [Theory]
    [InlineData(BatteryFlow.Discharging, "Discharging is forbidden", "The station reports that the battery may not discharge.", Tone.Critical)]
    [InlineData(BatteryFlow.Charging, "Charging is forbidden", "The station reports that the battery may not charge.", Tone.Warning)]
    public void Notify_FlowForbidden_SaysWhichOne(BatteryFlow flow, string title, string text, Tone tone)
    {
        // Act
        var notification = AlertPresenter.Notify(new FlowForbiddenAlert(flow), Now);

        // Assert
        notification.Should().Be(new Notification(title, text, tone));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Notify_StationSilent_TellsWhenItAnsweredLast(string language)
    {
        // Arrange
        Speak(language);
        var answeredAt = Moments.Local(day: 11, hour: 23, minute: 58);

        // Act
        var notification = AlertPresenter.Notify(new StaleDataAlert(answeredAt), Now);

        // Assert
        notification.Should().Be(new Notification(
            Translated("The station is not answering"),
            Translated("Latest answer: yesterday 23:58."),
            Tone.Warning));
    }

    [Fact]
    public void Notify_StationNeverAnswered_SaysSo()
    {
        // Act
        var notification = AlertPresenter.Notify(new StaleDataAlert(LatestAnswerAt: null), Now);

        // Assert
        notification.Text.Should().Be("No answer has come yet.");
    }
}

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Outages;


namespace MarstekOverlay.Desktop.Presentation;

public static class AlertPresenter
{
    /// <returns>Nothing for an alert the panel has a place of its own for: its heading tells of the power source
    /// and of stale data.</returns>
    public static OverlayAlert? DescribeOnPanel(Alert alert)
    {
        var text = alert switch
        {
            LowChargeAlert charge => Phrases.Format(
                charge.IsLowestThreshold ? Strings.Alert_CriticalChargeRow : Strings.Alert_LowChargeRow,
                Units.Percent(charge.ThresholdPercent)),
            BatteryTemperatureAlert temperature => Phrases.Format(
                temperature.IsTooHot ? Strings.Alert_TooHotRow : Strings.Alert_TooColdRow,
                Units.Celsius(temperature.Celsius),
                Units.Celsius(temperature.LimitCelsius)),
            FlowForbiddenAlert { Flow: BatteryFlow.Discharging } => Strings.Alert_DischargingForbiddenRow,
            FlowForbiddenAlert => Strings.Alert_ChargingForbiddenRow,
            _ => null,
        };

        return text is null ? null : new OverlayAlert(text, alert.Severity.Tone);
    }

    public static Notification Notify(Alert alert, DateTimeOffset now)
    {
        var (title, text) = alert switch
        {
            MainsLostAlert { IsSwitchSeen: false } found => (
                Strings.Alert_OnBatteryTitle,
                Phrases.Sentences(Strings.Alert_OnBatteryText, DescribeCharge(found.SocPercent))),
            MainsLostAlert lost => (
                Strings.Alert_MainsLostTitle,
                Phrases.Sentences(Strings.Alert_MainsLostText, DescribeCharge(lost.SocPercent))),
            MainsRestoredAlert restored => (
                Strings.Alert_MainsRestoredTitle,
                Phrases.Sentences(DescribeOutage(restored.Outage), DescribeCharge(restored.SocPercent))),
            LowChargeAlert charge => (
                charge.IsLowestThreshold ? Strings.Alert_CriticalChargeTitle : Strings.Alert_LowChargeTitle,
                Phrases.Format(Strings.Alert_LowChargeText, Units.Percent(charge.SocPercent), Units.Percent(charge.ThresholdPercent))),
            BatteryTemperatureAlert temperature => (
                Strings.Alert_TemperatureTitle,
                Phrases.Format(
                    temperature.IsTooHot ? Strings.Alert_TooHotText : Strings.Alert_TooColdText,
                    Units.Celsius(temperature.Celsius),
                    Units.Celsius(temperature.LimitCelsius))),
            FlowForbiddenAlert { Flow: BatteryFlow.Discharging } => (
                Strings.Alert_DischargingForbiddenTitle,
                Strings.Alert_DischargingForbiddenText),
            FlowForbiddenAlert => (Strings.Alert_ChargingForbiddenTitle, Strings.Alert_ChargingForbiddenText),
            StaleDataAlert stale => (
                Strings.Alert_StaleTitle,
                stale.LatestAnswerAt is { } answeredAt
                    ? Phrases.Format(Strings.Alert_StaleText, Phrases.Moment(answeredAt, now))
                    : Strings.Alert_StaleTextNoAnswer),
            // A balloon tip without a text is refused, so an alert nobody has worded yet still says its kind.
            _ => (Strings.Common_ProductName, alert.Kind.DisplayName),
        };

        return new Notification(title, text, alert.Severity.Tone);
    }

    private static string DescribeOutage(Outage? outage) =>
        outage is null
            ? Strings.Alert_MainsRestoredTextNoOutage
            : Phrases.Format(
                Strings.Alert_MainsRestoredText,
                OutagePhrases.Duration(outage),
                Units.Format(outage.ConsumedEnergy));

    private static string? DescribeCharge(double? socPercent) =>
        socPercent is { } soc ? Phrases.Format(Strings.Alert_ChargeNow, Units.Percent(soc)) : null;
}

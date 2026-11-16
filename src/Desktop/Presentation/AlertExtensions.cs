using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring.Alerts;

namespace MarstekOverlay.Desktop.Presentation;

public static class AlertExtensions
{
    extension(AlertKind kind)
    {
        public string DisplayName => kind switch
        {
            AlertKind.MainsLost => Strings.AlertKind_MainsLost,
            AlertKind.MainsRestored => Strings.AlertKind_MainsRestored,
            AlertKind.LowCharge => Strings.AlertKind_LowCharge,
            AlertKind.BatteryTemperature => Strings.AlertKind_BatteryTemperature,
            AlertKind.FlowForbidden => Strings.AlertKind_FlowForbidden,
            AlertKind.StaleData => Strings.AlertKind_StaleData,
            _ => kind.ToString(),
        };
    }

    extension(AlertSeverity severity)
    {
        public Tone Tone => severity switch
        {
            AlertSeverity.Critical => Tone.Critical,
            AlertSeverity.Warning => Tone.Warning,
            _ => Tone.Info,
        };
    }

    extension(ChargeLevel level)
    {
        public Tone Tone => level switch
        {
            ChargeLevel.Critical => Tone.Critical,
            ChargeLevel.Low => Tone.Warning,
            _ => Tone.Positive,
        };
    }
}

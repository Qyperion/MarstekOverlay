using MarstekOverlay.Desktop.Localization;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>The rows below the charge bar, in the order they are shown. Saved by name in the user settings.</summary>
public enum OverlayRowKind
{
    Forecast,
    Battery,
    StoredEnergy,
    CurrentOutage,
    LatestOutage,
    Grid,
    BackupOutput,
    Solar,
    BatteryTemperature,
    WifiSignal,
    OperatingMode,
}

public static class OverlayRowKindExtensions
{
    extension(OverlayRowKind kind)
    {
        public string DisplayName => kind switch
        {
            OverlayRowKind.Forecast => Strings.Row_Forecast,
            OverlayRowKind.Battery => Strings.Row_Battery,
            OverlayRowKind.StoredEnergy => Strings.Row_StoredEnergy,
            OverlayRowKind.CurrentOutage => Strings.Row_CurrentOutage,
            OverlayRowKind.LatestOutage => Strings.Row_LatestOutage,
            OverlayRowKind.Grid => Strings.Row_Grid,
            OverlayRowKind.BackupOutput => Strings.Row_BackupOutput,
            OverlayRowKind.Solar => Strings.Row_Solar,
            OverlayRowKind.BatteryTemperature => Strings.Row_BatteryTemperature,
            OverlayRowKind.WifiSignal => Strings.Row_WifiSignal,
            OverlayRowKind.OperatingMode => Strings.Row_OperatingMode,
            _ => kind.ToString(),
        };

        /// <summary>Any other row stays off the panel until it is asked for in the settings.</summary>
        public bool IsShownByDefault => kind
            is OverlayRowKind.Forecast
            or OverlayRowKind.Battery
            or OverlayRowKind.Grid
            or OverlayRowKind.BackupOutput
            or OverlayRowKind.Solar;
    }
}

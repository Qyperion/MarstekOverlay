using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;

using Microsoft.Extensions.Logging;

using System.Numerics;

namespace MarstekOverlay.Desktop.Settings;

/// <summary>Brings what a file edited by hand holds to what the app can work with: a part that is missing takes
/// its defaults, a number outside what the settings window accepts is pulled to the nearest bound, a chart period
/// it does not offer becomes the one it marks. An address that is none is only told of.</summary>
public static class UserSettingsRepair
{
    extension(UserSettings settings)
    {
        /// <param name="logger">Told of every value put right.</param>
        public void Repair(ILogger logger)
        {
            settings.Language = Present(settings.Language, nameof(UserSettings.Language), string.Empty, logger);

            settings.Station = Present(settings.Station, nameof(UserSettings.Station), new StationSettings(), logger);
            settings.Station.Host = Present(settings.Station.Host, "Station.Host", string.Empty, logger);
            settings.Station.Check(logger);

            settings.Overlay = Present(settings.Overlay, nameof(UserSettings.Overlay), new OverlaySettings(), logger);
            settings.Overlay.Repair(logger);

            settings.Forecast = Present(settings.Forecast, nameof(UserSettings.Forecast), new ForecastSettings(), logger);
            settings.Forecast.Repair(logger);

            settings.Alerts = Present(settings.Alerts, nameof(UserSettings.Alerts), new AlertSettings(), logger);
            settings.Alerts.Repair(logger);

            settings.Details = Present(settings.Details, nameof(UserSettings.Details), new DetailsSettings(), logger);
        }
    }

    extension(StationSettings station)
    {
        // Left as it is, like the file: the settings window marks it, and whoever fixes it there fixes the file.
        private void Check(ILogger logger)
        {
            if (!string.IsNullOrWhiteSpace(station.Host) && !StationEndpoint.TryParseAddress(station.Host, out _))
                logger.LogWarning("Settings value {Name} = {Value} is not an address of four decimal numbers, no station is polled", "Station.Host", station.Host);

            if (!StationEndpoint.IsValidPort(station.Port))
                logger.LogWarning("Settings value {Name} = {Value} is not a port, no station is polled", "Station.Port", station.Port);
        }
    }

    extension(OverlaySettings overlay)
    {
        private void Repair(ILogger logger)
        {
            overlay.HiddenRows = Present(overlay.HiddenRows, "Overlay.HiddenRows", [], logger);
            overlay.ShownRows = Present(overlay.ShownRows, "Overlay.ShownRows", [], logger);
            overlay.OpacityPercent = Within(overlay.OpacityPercent, OverlaySettings.MinOpacityPercent, OverlaySettings.MaxOpacityPercent, "Overlay.OpacityPercent", logger);
            overlay.ScalePercent = Within(overlay.ScalePercent, OverlaySettings.MinScalePercent, OverlaySettings.MaxScalePercent, "Overlay.ScalePercent", logger);
            overlay.ChartHours = Offered(overlay.ChartHours, logger);
        }
    }

    extension(ForecastSettings forecast)
    {
        private void Repair(ILogger logger)
        {
            forecast.ReserveSocPercent = Within(forecast.ReserveSocPercent, ForecastSettings.MinReserveSocPercent, ForecastSettings.MaxReserveSocPercent, "Forecast.ReserveSocPercent", logger);
            forecast.DischargeEfficiencyPercent = Within(forecast.DischargeEfficiencyPercent, ForecastSettings.MinEfficiencyPercent, ForecastSettings.MaxEfficiencyPercent, "Forecast.DischargeEfficiencyPercent", logger);
            forecast.ChargeEfficiencyPercent = Within(forecast.ChargeEfficiencyPercent, ForecastSettings.MinEfficiencyPercent, ForecastSettings.MaxEfficiencyPercent, "Forecast.ChargeEfficiencyPercent", logger);
        }
    }

    extension(AlertSettings alerts)
    {
        private void Repair(ILogger logger)
        {
            alerts.DisabledKinds = Present(alerts.DisabledKinds, "Alerts.DisabledKinds", [], logger);
            alerts.ChargeThresholdsPercent = Present(alerts.ChargeThresholdsPercent, "Alerts.ChargeThresholdsPercent", [], logger);

            var outside = alerts.ChargeThresholdsPercent
                .Where(threshold => threshold is < AlertSettings.MinChargeThresholdPercent or > AlertSettings.MaxChargeThresholdPercent)
                .ToList();
            if (outside.Count > 0)
            {
                logger.LogWarning("Settings value {Name} held {Values} outside {Min}..{Max}, left out", "Alerts.ChargeThresholdsPercent", outside, AlertSettings.MinChargeThresholdPercent, AlertSettings.MaxChargeThresholdPercent);
                alerts.ChargeThresholdsPercent.RemoveAll(outside.Contains);
            }

            alerts.MinBatteryTemperatureCelsius = Within(alerts.MinBatteryTemperatureCelsius, AlertSettings.MinTemperatureLimitCelsius, AlertSettings.MaxTemperatureLimitCelsius, "Alerts.MinBatteryTemperatureCelsius", logger);
            alerts.MaxBatteryTemperatureCelsius = Within(alerts.MaxBatteryTemperatureCelsius, AlertSettings.MinTemperatureLimitCelsius, AlertSettings.MaxTemperatureLimitCelsius, "Alerts.MaxBatteryTemperatureCelsius", logger);
            if (alerts.MinBatteryTemperatureCelsius >= alerts.MaxBatteryTemperatureCelsius)
            {
                var defaults = new AlertSettings();
                logger.LogWarning(
                    "Settings battery temperature limits {Min}..{Max} leave no range, using {DefaultMin}..{DefaultMax}",
                    alerts.MinBatteryTemperatureCelsius,
                    alerts.MaxBatteryTemperatureCelsius,
                    defaults.MinBatteryTemperatureCelsius,
                    defaults.MaxBatteryTemperatureCelsius);
                alerts.MinBatteryTemperatureCelsius = defaults.MinBatteryTemperatureCelsius;
                alerts.MaxBatteryTemperatureCelsius = defaults.MaxBatteryTemperatureCelsius;
            }

            alerts.StaleAfterMinutes = Within(alerts.StaleAfterMinutes, AlertSettings.MinStaleAfterMinutes, AlertSettings.MaxStaleAfterMinutes, "Alerts.StaleAfterMinutes", logger);
        }
    }

    // The file can say null for anything, whatever the property promises.
    private static T Present<T>(T? value, string name, T fallback, ILogger logger)
        where T : class
    {
        if (value is not null)
            return value;

        logger.LogWarning("Settings value {Name} is missing, using the default", name);
        return fallback;
    }

    private static int Offered(int chartHours, ILogger logger)
    {
        var offered = OverlaySettings.OfferedChartHours(chartHours);
        if (offered != chartHours)
            logger.LogWarning("Settings value {Name} = {Value} is not one of {Choices}, using {Offered}", "Overlay.ChartHours", chartHours, OverlaySettings.ChartHoursChoices, offered);

        return offered;
    }

    private static T Within<T>(T value, T min, T max, string name, ILogger logger)
        where T : INumber<T>
    {
        var within = T.Clamp(value, min, max);
        if (within != value)
            logger.LogWarning("Settings value {Name} = {Value} is outside {Min}..{Max}, using {Within}", name, value, min, max, within);

        return within;
    }
}

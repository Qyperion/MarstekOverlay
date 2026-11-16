using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Settings;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>The typed fields of the settings window: what a text stands for, or what to say of it when it stands
/// for nothing that may be saved. The limits are the ones a hand-edited file is held to.</summary>
public static class SettingsInput
{
    private static readonly char[] ThresholdSeparators = [' ', ';'];

    public static bool TryReadPort(string text, out int port, [NotNullWhen(false)] out string? error)
    {
        var isValid = int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port)
            && StationEndpoint.IsValidPort(port);

        error = isValid ? null : Strings.Settings_InvalidPort;
        return isValid;
    }

    public static bool TryReadAddress(
        string text,
        [NotNullWhen(true)] out IPAddress? address,
        [NotNullWhen(false)] out string? error)
    {
        var isValid = StationEndpoint.TryParseAddress(text, out address);

        error = isValid ? null : Strings.Settings_InvalidAddress;
        return isValid;
    }

    public static bool TryReadReserve(string text, out double percent, [NotNullWhen(false)] out string? error) =>
        TryReadWithin(
            text,
            ForecastSettings.MinReserveSocPercent,
            ForecastSettings.MaxReserveSocPercent,
            Strings.Settings_InvalidReserve,
            out percent,
            out error);

    public static bool TryReadEfficiency(string text, out double percent, [NotNullWhen(false)] out string? error) =>
        TryReadWithin(
            text,
            ForecastSettings.MinEfficiencyPercent,
            ForecastSettings.MaxEfficiencyPercent,
            Strings.Settings_InvalidEfficiency,
            out percent,
            out error);

    public static bool TryReadStaleAfter(string text, out double minutes, [NotNullWhen(false)] out string? error) =>
        TryReadWithin(
            text,
            AlertSettings.MinStaleAfterMinutes,
            AlertSettings.MaxStaleAfterMinutes,
            Strings.Settings_InvalidStaleAfter,
            out minutes,
            out error);

    /// <summary>What the silence typed comes to under the poll, when that is longer; nothing while it is not, or
    /// while the text stands for no silence that may be saved.</summary>
    public static string? StaleAfterInEffect(string text, PollingSettings polling)
    {
        if (!TryReadStaleAfter(text, out var minutes, out _))
            return null;

        var limit = SilenceLimit.Of(minutes, polling);

        return limit > TimeSpan.FromMinutes(minutes) ? Phrases.Format(Strings.Settings_StaleAfterInEffect, limit.TotalMinutes) : null;
    }

    /// <param name="thresholds">Highest first, the order they are passed in on the way down.</param>
    public static bool TryReadChargeThresholds(
        string text,
        out IReadOnlyList<double> thresholds,
        [NotNullWhen(false)] out string? error)
    {
        var read = new List<double>();

        // The comma is a decimal one here as in every other field; one typed after a threshold out of habit,
        // as in "50, 30", still ends it.
        foreach (var part in text.Split(ThresholdSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var number = part.TrimEnd(',');
            if (number.Length == 0)
                continue;

            if (!TryParseNumber(number, out var threshold)
                || threshold is < AlertSettings.MinChargeThresholdPercent or > AlertSettings.MaxChargeThresholdPercent)
            {
                thresholds = [];
                error = Phrases.Format(
                    Strings.Settings_InvalidChargeThresholds,
                    AlertSettings.MinChargeThresholdPercent,
                    AlertSettings.MaxChargeThresholdPercent);
                return false;
            }

            read.Add(threshold);
        }

        thresholds = [.. read.Distinct().OrderDescending()];
        error = null;
        return true;
    }

    public static bool TryReadTemperatureLimits(
        string lowestText,
        string highestText,
        out double lowest,
        out double highest,
        [NotNullWhen(false)] out string? error)
    {
        var isValid = TryParseNumber(lowestText, out lowest, NumberStyles.AllowLeadingSign)
            & TryParseNumber(highestText, out highest, NumberStyles.AllowLeadingSign)
            && lowest >= AlertSettings.MinTemperatureLimitCelsius
            && highest <= AlertSettings.MaxTemperatureLimitCelsius
            && lowest < highest;

        error = isValid
            ? null
            : Phrases.Format(
                Strings.Settings_InvalidTemperatureLimits,
                AlertSettings.MinTemperatureLimitCelsius,
                AlertSettings.MaxTemperatureLimitCelsius);
        return isValid;
    }

    /// <summary>A number the way the fields take it back.</summary>
    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <param name="thresholds">In any order; they are shown highest first.</param>
    public static string ChargeThresholds(IEnumerable<double> thresholds) =>
        string.Join("; ", thresholds.OrderDescending().Select(Number));

    private static bool TryReadWithin(
        string text,
        double min,
        double max,
        string template,
        out double value,
        [NotNullWhen(false)] out string? error)
    {
        var isValid = TryParseNumber(text, out value) && value >= min && value <= max;

        error = isValid ? null : Phrases.Format(template, min, max);
        return isValid;
    }

    // A decimal comma is what half of the keyboards in use type. "NaN" and "Infinity" parse as well, and the
    // first of them is never below or above a limit.
    private static bool TryParseNumber(string text, out double value, NumberStyles sign = NumberStyles.None) =>
        double.TryParse(
            text.Trim().Replace(',', '.'),
            NumberStyles.AllowDecimalPoint | sign,
            CultureInfo.InvariantCulture,
            out value)
        && double.IsFinite(value);
}

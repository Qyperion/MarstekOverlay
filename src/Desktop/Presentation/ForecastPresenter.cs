using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Forecast;

using System.Globalization;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Words the forecast of the panel: in full on its row, briefly on the compact line, and how it is made.</summary>
public static class ForecastPresenter
{
    private static readonly TimeSpan ForecastStep = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LongestForecast = TimeSpan.FromDays(7);

    private const string WeekdayFormat = "ddd";

    public static string Describe(RuntimeForecast forecast, ForecastSettings settings, DateTimeOffset now)
    {
        var reserve = Units.Percent(settings.ReserveSocPercent);
        var full = Units.Percent(Percent.Whole);

        return forecast switch
        {
            { Status: ForecastStatus.TimeToReserve, EndsAt: { } endsAt } =>
                DescribeInFull(Strings.Forecast_Remaining, endsAt - now, reserve, DescribeClockTime(endsAt, now)),
            { Status: ForecastStatus.TimeToFull, EndsAt: { } endsAt } =>
                DescribeInFull(Strings.Forecast_ChargeRemaining, endsAt - now, full, DescribeClockTime(endsAt, now)),
            { Status: ForecastStatus.Standby, StandbyRuntime: { } runtime } =>
                DescribeInFull(Strings.Forecast_Standby, runtime, reserve),
            { Status: ForecastStatus.Collecting } => Strings.Forecast_Collecting,
            { Status: ForecastStatus.ReserveReached } => Phrases.Format(Strings.Forecast_ReserveReached, reserve),
            { Status: ForecastStatus.LoadTooLow } => Strings.Forecast_LoadTooLow,
            _ => Strings.Forecast_NotEnoughData,
        };
    }

    /// <returns>Nothing while there is no estimate: a single line has no room to say why.</returns>
    public static string? DescribeBriefly(RuntimeForecast forecast, DateTimeOffset now) => forecast switch
    {
        { Status: ForecastStatus.TimeToReserve, EndsAt: { } endsAt } => DescribeBriefly(Strings.Compact_Remaining, endsAt - now),
        { Status: ForecastStatus.TimeToFull, EndsAt: { } endsAt } => DescribeBriefly(Strings.Compact_ChargeRemaining, endsAt - now),
        { Status: ForecastStatus.Standby, StandbyRuntime: { } runtime } => DescribeBriefly(Strings.Compact_Remaining, runtime),
        _ => null,
    };

    public static string DescribeHint(RuntimeForecast forecast, ForecastSettings settings)
    {
        var averagedOverMinutes = RuntimeForecaster.AveragingWindow.TotalMinutes;
        var delivered = Units.Percent(settings.DischargeEfficiencyPercent);
        var stored = Units.Percent(settings.ChargeEfficiencyPercent);
        var reserve = Units.Percent(settings.ReserveSocPercent);

        return forecast.Status switch
        {
            ForecastStatus.TimeToFull => Phrases.Format(Strings.Forecast_ChargeHint, averagedOverMinutes, stored),
            ForecastStatus.Standby => Phrases.Format(Strings.Forecast_StandbyHint, averagedOverMinutes, delivered, reserve),
            _ => Phrases.Format(Strings.Forecast_Hint, averagedOverMinutes, delivered, reserve),
        };
    }

    /// <param name="template">Takes the time left and, for an estimate that ends at a known moment,
    /// <paramref name="clockTime"/>.</param>
    /// <param name="target">The level the estimate runs to, named when the time is out of the range worth showing.</param>
    private static string DescribeInFull(string template, TimeSpan remaining, string target, string? clockTime = null) =>
        DescribeEstimate(
            remaining,
            within: time => Phrases.Format(template, time, clockTime),
            lessThan: minutes => Phrases.Format(Strings.Forecast_LessThan, minutes, target),
            moreThan: days => Phrases.Format(Strings.Forecast_MoreThan, days, target));

    private static string DescribeBriefly(string template, TimeSpan remaining) =>
        DescribeEstimate(
            remaining,
            within: time => Phrases.Format(template, time),
            lessThan: minutes => Phrases.Format(Strings.Compact_LessThan, minutes),
            moreThan: days => Phrases.Format(Strings.Compact_MoreThan, days));

    private static string DescribeEstimate(
        TimeSpan remaining,
        Func<string, string> within,
        Func<double, string> lessThan,
        Func<double, string> moreThan)
    {
        if (remaining > LongestForecast)
            return moreThan(LongestForecast.TotalDays);

        var rounded = ForecastStep * Math.Round(remaining / ForecastStep, MidpointRounding.AwayFromZero);

        return rounded < ForecastStep ? lessThan(ForecastStep.TotalMinutes) : within(Phrases.HoursAndMinutes(rounded));
    }

    private static string DescribeClockTime(DateTimeOffset moment, DateTimeOffset now)
    {
        var local = moment.ToLocalTime().DateTime;
        var rounded = new DateTime((local.Ticks + ForecastStep.Ticks / 2) / ForecastStep.Ticks * ForecastStep.Ticks);
        var time = rounded.ToString(Phrases.ClockFormat, CultureInfo.InvariantCulture);

        return (rounded.Date - now.ToLocalTime().Date).Days switch
        {
            <= 0 => time,
            1 => Phrases.Format(Strings.Forecast_Tomorrow, time),
            // The name of a day is text, so it follows the language of the app rather than the regional format.
            _ => Phrases.Format(Strings.Forecast_OnDay, rounded.ToString(WeekdayFormat, Strings.Culture), time),
        };
    }
}

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Outages;

using System.Globalization;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Wordings of time, and ways of joining texts, shared by the panel and the notifications.</summary>
public static class Phrases
{
    public const string ClockFormat = "HH:mm";

    private const string ClockToSecondFormat = "HH:mm:ss";

    private const string DateFormat = "d MMM";
    private const string DateWithYearFormat = "d MMM yyyy";
    private const string MonthFormat = "MMMM yyyy";
    private const string ReadingSeparator = " · ";
    private const int MinutesPerHour = 60;
    private const int SecondsPerMinute = 60;

    // Power is shown in whole watts, and less than half of one reads as none.
    private const double WattsShown = 0.5;

    // Up to two days a period reads better in hours.
    private static readonly TimeSpan PeriodInDaysFrom = TimeSpan.FromDays(2);

    /// <param name="isExact">False for a period seen only in part, which lasted at least this long.</param>
    public static string Duration(TimeSpan duration, bool isExact)
    {
        var minutes = (long)Math.Max(duration.TotalMinutes, 0);

        // Not "at least" either: a bound of no minutes would tell nothing.
        if (minutes == 0)
            return Strings.Duration_UnderMinute;

        var text = minutes >= MinutesPerHour
            ? HoursAndMinutes(TimeSpan.FromMinutes(minutes))
            : Format(Strings.Duration_Minutes, minutes);

        return isExact ? text : Format(Strings.Duration_AtLeast, text);
    }

    /// <summary>Hours and minutes, the hours named even when there are none.</summary>
    public static string HoursAndMinutes(TimeSpan duration) =>
        Format(Strings.Duration_HoursMinutes, (int)duration.TotalHours, duration.Minutes);

    /// <summary>How long ago, to the second while that is under a minute.</summary>
    public static string Ago(TimeSpan age)
    {
        var seconds = (long)Math.Max(age.TotalSeconds, 0);

        return seconds switch
        {
            0 => Strings.Age_JustNow,
            < SecondsPerMinute => Format(Strings.Age_Ago, Format(Strings.Duration_Seconds, seconds)),
            _ => Format(Strings.Age_Ago, Duration(age, isExact: true)),
        };
    }

    /// <summary>A moment in the past, with its day named unless it is today.</summary>
    public static string Moment(DateTimeOffset moment, DateTimeOffset now) => Moment(moment, now, yearInView: null);

    /// <param name="yearInView">The year a heading over the moment names; a date of any other year names its own.</param>
    public static string Moment(DateTimeOffset moment, DateTimeOffset now, int? yearInView)
    {
        var local = moment.ToLocalTime().DateTime;
        var time = local.ToString(ClockFormat, CultureInfo.InvariantCulture);
        var dateFormat = yearInView is { } year && local.Year != year ? DateWithYearFormat : DateFormat;

        return (now.ToLocalTime().Date - local.Date).Days switch
        {
            <= 0 => time,
            1 => Format(Strings.Moment_Yesterday, time),
            // The name of a month is text, so it follows the language of the app rather than the regional format.
            _ => Format(Strings.Moment_OnDate, local.ToString(dateFormat, Strings.Culture), time),
        };
    }

    public static string TimeOfDay(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString(ClockFormat, CultureInfo.InvariantCulture);

    /// <summary>The time of an answer: the station answers every few seconds, so minutes would not tell two apart.</summary>
    public static string TimeOfAnswer(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString(ClockToSecondFormat, CultureInfo.InvariantCulture);

    public static string AnsweredAt(DateTimeOffset moment) => Format(Strings.Common_AnsweredAt, TimeOfAnswer(moment));

    public static string Date(DateTimeOffset moment) => moment.ToLocalTime().ToString(DateFormat, Strings.Culture);

    /// <summary>A month as a heading: named in full and with a capital letter, whatever the language does with
    /// the names of months inside a sentence.</summary>
    public static string Month(CalendarMonth month)
    {
        var name = new DateTime(month.Year, month.Month, 1).ToString(MonthFormat, Strings.Culture);

        return Strings.Culture.TextInfo.ToUpper(name[0]) + name[1..];
    }

    /// <summary>How far back a chart reaches.</summary>
    public static string Period(TimeSpan period) =>
        period > PeriodInDaysFrom ? Format(Strings.Chart_Days, period.TotalDays) : Format(Strings.Chart_Hours, period.TotalHours);

    public static string PeriodOfHours(int hours) => Period(TimeSpan.FromHours(hours));

    public static string Sentences(params string?[] sentences) =>
        string.Join(' ', sentences.Where(sentence => sentence is not null));

    public static string Lines(params string?[] lines) =>
        string.Join(Environment.NewLine, lines.Where(line => line is not null));

    /// <summary>Short readings side by side on one line.</summary>
    public static string Readings(params string?[] readings) =>
        string.Join(ReadingSeparator, readings.Where(reading => reading is not null));

    /// <summary>Parts of one value, such as the voltage and the current of a solar input.</summary>
    public static string List(params string?[] parts) =>
        string.Join(Strings.Common_ListSeparator, parts.Where(part => part is not null));

    /// <summary>A reading under its name, for a place with no column to put the name in.</summary>
    public static string Labelled(string label, string value) => Format(Strings.Common_Labelled, label, value);

    /// <summary>What the battery takes or gives, told by the direction rather than by the sign, which a reader
    /// would have to know the meaning of.</summary>
    /// <returns>Nothing for a battery that does neither.</returns>
    public static string? BatteryPower(Power power) => power.Watts switch
    {
        >= WattsShown => Format(Strings.Battery_Charging, Units.Format(power)),
        <= -WattsShown => Format(Strings.Battery_Discharging, Units.Format(-power)),
        _ => null,
    };

    /// <summary>Fills a text of <see cref="Strings"/> in. Numbers follow the regional format of Windows.</summary>
    internal static string Format(string template, params ReadOnlySpan<object?> arguments) =>
        string.Format(CultureInfo.CurrentCulture, template, arguments);
}

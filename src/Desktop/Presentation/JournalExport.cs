using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring.Outages;

using System.Globalization;
using System.IO;
using System.Text;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>The outages of a month as a CSV file Excel opens with a double click. Excel reads such a file by
/// the regional format of Windows, so the separator, the dates and the decimal mark follow that format rather
/// than the language of the app; the column titles are text and follow the language. An outage that crosses into
/// the month before or after is cut at the bound and marked, so that the files of two months add up.</summary>
public static class JournalExport
{
    private const string LineBreak = "\r\n";
    private const string Quote = "\"";
    private const string EscapedQuote = "\"\"";
    private const string NumberFormat = "0.#";
    private const string FileNameFormat = "marstek-outages-{0:0000}-{1:00}.csv";

    // Around the clock, as an "AM" or a "PM" is one more thing for Excel to fail to recognise.
    private const string TimeOfDayFormat = " HH:mm:ss";

    // Without the mark Excel takes the file for one in the legacy code page and garbles everything but Latin.
    private static readonly UTF8Encoding Utf8WithMark = new(encoderShouldEmitUTF8Identifier: true);

    public static string SuggestFileName(CalendarMonth month) =>
        string.Format(CultureInfo.InvariantCulture, FileNameFormat, month.Year, month.Month);

    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be written there.</exception>
    public static void Save(string path, IEnumerable<OutagePart> outages) =>
        File.WriteAllText(path, Compose(outages), Utf8WithMark);

    /// <returns>A line of titles and a line for every outage, the earliest first.</returns>
    public static string Compose(IEnumerable<OutagePart> outages)
    {
        var format = CultureInfo.CurrentCulture;
        var separator = format.TextInfo.ListSeparator;

        string[] titles =
        [
            Strings.Journal_Started,
            Strings.Journal_Ended,
            Strings.Journal_Duration,
            Strings.Export_Energy,
            Strings.Export_StartCharge,
            Strings.Export_EndCharge,
            Strings.Export_StartUnseen,
            Strings.Export_EndUnseen,
            Strings.Export_MonthBound,
        ];

        var lines = outages
            .OrderBy(outage => outage.StartedAt)
            .Select(outage => Describe(outage, format))
            .Prepend(titles)
            .Select(cells => string.Join(separator, cells.Select(cell => Escape(cell, separator))));

        return string.Concat(lines.Select(line => line + LineBreak));
    }

    // The charge and whether an end was seen belong to the ends of the outage, which a bound of the month hides.
    private static string[] Describe(OutagePart part, CultureInfo format)
    {
        var outage = part.Outage;

        return
        [
            Describe(part.StartedAt, format),
            outage.IsOngoing && !part.WentOnLater ? Strings.Journal_Ongoing : Describe(part.EndedAt, format),
            Describe(part.Duration, format),
            part.ConsumedEnergy.WattHours.ToString(NumberFormat, format),
            part.BeganEarlier ? string.Empty : Describe(outage.StartSocPercent, format),
            part.WentOnLater ? string.Empty : Describe(outage.EndSocPercent, format),
            Mark(!part.BeganEarlier && !outage.IsStartObserved),
            Mark(!part.WentOnLater && !outage.IsOngoing && !outage.IsEndObserved),
            Phrases.List(part.BeganEarlier ? Strings.Export_BeganEarlier : null, part.WentOnLater ? Strings.Export_WentOnLater : null),
        ];
    }

    private static string Describe(double? percent, CultureInfo format) => percent?.ToString(NumberFormat, format) ?? string.Empty;

    private static string Describe(DateTimeOffset moment, CultureInfo format) =>
        moment.ToLocalTime().DateTime.ToString(format.DateTimeFormat.ShortDatePattern + TimeOfDayFormat, format);

    // Hours may run past a day, which no format of a time of day shows.
    private static string Describe(TimeSpan duration, CultureInfo format)
    {
        var separator = format.DateTimeFormat.TimeSeparator;
        var lasted = duration > TimeSpan.Zero ? duration : TimeSpan.Zero;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(long)lasted.TotalHours}{separator}{lasted.Minutes:00}{separator}{lasted.Seconds:00}");
    }

    private static string Mark(bool isSet) => isSet ? Strings.Common_Yes : string.Empty;

    private static string Escape(string cell, string separator) =>
        cell.Contains(separator, StringComparison.Ordinal) || cell.Contains(Quote, StringComparison.Ordinal) || cell.AsSpan().IndexOfAny('\r', '\n') >= 0
            ? Quote + cell.Replace(Quote, EscapedQuote, StringComparison.Ordinal) + Quote
            : cell;
}

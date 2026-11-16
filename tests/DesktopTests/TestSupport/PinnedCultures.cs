using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>Cultures whose names of months and days, first day of the week and separators come from
/// <c>TestSupport/Cultures.resx</c> rather than from the culture data of the machine.</summary>
internal static class PinnedCultures
{
    private const char ListSeparator = '|';

    private static readonly ResourceManager Data =
        new($"{typeof(PinnedCultures).Namespace}.Cultures", typeof(PinnedCultures).Assembly);

    private static readonly ConcurrentDictionary<string, CultureInfo> Cultures = new();

    /// <param name="name">A language such as "uk", or a regional format such as "uk-UA".</param>
    public static CultureInfo Get(string name) => Cultures.GetOrAdd(name, Pin);

    private static CultureInfo Pin(string name)
    {
        var culture = new CultureInfo(name, useUserOverride: false);
        var language = culture.TwoLetterISOLanguageName;

        var dates = culture.DateTimeFormat;
        dates.MonthNames = Names(language, nameof(dates.MonthNames));
        dates.MonthGenitiveNames = Names(language, nameof(dates.MonthGenitiveNames));
        dates.AbbreviatedMonthNames = Names(language, nameof(dates.AbbreviatedMonthNames));
        dates.AbbreviatedMonthGenitiveNames = Names(language, nameof(dates.AbbreviatedMonthGenitiveNames));
        dates.DayNames = Names(language, nameof(dates.DayNames));
        dates.AbbreviatedDayNames = Names(language, nameof(dates.AbbreviatedDayNames));
        dates.ShortestDayNames = Names(language, nameof(dates.ShortestDayNames));
        dates.FirstDayOfWeek = Enum.Parse<DayOfWeek>(Value(language, nameof(dates.FirstDayOfWeek)));
        dates.ShortDatePattern = Value(language, nameof(dates.ShortDatePattern));
        dates.DateSeparator = Value(language, nameof(dates.DateSeparator));
        dates.TimeSeparator = Value(language, nameof(dates.TimeSeparator));

        culture.TextInfo.ListSeparator = Value(language, nameof(TextInfo.ListSeparator));

        var numbers = culture.NumberFormat;
        numbers.NumberDecimalSeparator = numbers.PercentDecimalSeparator = Value(language, "DecimalSeparator");
        numbers.NumberGroupSeparator = numbers.PercentGroupSeparator = Value(language, "GroupSeparator");
        numbers.NegativeSign = Value(language, nameof(numbers.NegativeSign));

        return CultureInfo.ReadOnly(culture);
    }

    private static string[] Names(string language, string property) => Value(language, property).Split(ListSeparator);

    private static string Value(string language, string property) =>
        Data.GetString($"{language}.{property}", CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Cultures.resx has no {property} for \"{language}\".");
}

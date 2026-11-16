using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;

using System.Globalization;

namespace MarstekOverlay.Desktop.Presentation;

/// <param name="Hint">What is not known about the outage, when it was seen only in part.</param>
public sealed record OutageEntry(
    string Started,
    string Ended,
    string Duration,
    string Energy,
    string Charge,
    string? Hint,
    bool IsOngoing)
{
    // What a screen reader calls the row.
    public override string ToString() => Phrases.Readings(Started, Ended, Duration, Energy, Charge);
}

/// <param name="Headline">The time on the battery, or that there was none.</param>
/// <param name="Details">Nothing for a period without outages.</param>
public sealed record OutageTotalsCard(string Title, string Headline, string? Details)
{
    // What a screen reader calls the card.
    public override string ToString() => Phrases.Readings(Title, Headline, Details);
}

/// <summary>Words the outage journal and the charts of the details window.</summary>
public static class DetailsPresenter
{
    /// <summary>How far back the totals reach. A week may begin in the month before, so neither of the two
    /// longer periods always holds the other.</summary>
    public static DateTimeOffset TotalsFrom(DateTimeOffset now)
    {
        var week = WeekStart(now);
        var month = CalendarPeriods.MonthStart(now, TimeZoneInfo.Local);

        return week < month ? week : month;
    }

    /// <summary>Where today, this week and this month begin: an outage that crosses one of them counts towards
    /// the totals on either side with its own part.</summary>
    public static IReadOnlyList<DateTimeOffset> TotalsBounds(DateTimeOffset now) =>
        [CalendarPeriods.DayStart(now, TimeZoneInfo.Local), WeekStart(now), CalendarPeriods.MonthStart(now, TimeZoneInfo.Local)];

    /// <summary>What today, this week and this month add up to so far. The week begins on the day the regional
    /// format of Windows names as its first.</summary>
    /// <param name="outages">Those since <see cref="TotalsFrom"/> at the least.</param>
    /// <param name="load">Through the outages that cross <see cref="TotalsBounds"/>.</param>
    public static IReadOnlyList<OutageTotalsCard> DescribeTotals(IReadOnlyList<Outage> outages, DateTimeOffset now, RecordedLoad load) =>
    [
        DescribeTotals(Strings.Journal_Today, OutageTotals.Over(outages, CalendarPeriods.DayStart(now, TimeZoneInfo.Local), now, load)),
        DescribeTotals(Strings.Journal_ThisWeek, OutageTotals.Over(outages, WeekStart(now), now, load)),
        DescribeTotals(Strings.Journal_ThisMonth, OutageTotals.Over(outages, CalendarPeriods.MonthStart(now, TimeZoneInfo.Local), now, load)),
    ];

    /// <summary>The outages of a month, each whole even where it crosses into the month before or after. A date of
    /// another year than the month's names its year.</summary>
    public static IReadOnlyList<OutageEntry> DescribeOutages(IReadOnlyList<Outage> outages, CalendarMonth month, DateTimeOffset now) =>
        outages.Select(outage => Describe(outage, month.Year, now)).ToList();

    /// <summary>What a month adds up to, counting only the part of an outage that falls into it.</summary>
    /// <param name="load">Through the outages that cross the bounds of the month.</param>
    /// <returns>Nothing for the current month, which the totals already tell, and for a month without outages.</returns>
    public static string? DescribeMonthTotal(IReadOnlyList<Outage> outages, CalendarMonth month, DateTimeOffset now, RecordedLoad load)
    {
        var zone = TimeZoneInfo.Local;
        if (month == CalendarMonth.Of(now, zone))
            return null;

        var totals = OutageTotals.Over(outages, month.StartsAt(zone), month.EndsAt(zone), load);

        return totals.Count == 0
            ? null
            : Phrases.Labelled(
                Strings.Journal_MonthTotal,
                Phrases.Readings(
                    Phrases.Duration(totals.Duration, totals.IsDurationExact),
                    Phrases.Format(Strings.Journal_Count, totals.Count),
                    Units.Format(totals.ConsumedEnergy)));
    }

    /// <param name="pointer">The moment under the pointer, whose values take the place of the summary.</param>
    public static string DescribeChart(HistoryChart chart, DateTimeOffset? pointer, DateTimeOffset now)
    {
        if (pointer is not { } moment || chart.ReadAt(moment) is not { } reading)
            return OverlayPresenter.SummarizeLoad(chart);

        return Phrases.Format(
            Strings.Details_ChartMoment,
            Phrases.Moment(reading.At, now),
            Phrases.Readings(
                DescribeLoad(reading),
                Phrases.Format(Strings.Details_ChartCharge, reading.SocPercent is { } soc ? Units.Percent(soc) : StationSections.NoValue),
                DescribeBattery(reading.BatteryPower),
                reading.IsOnBattery ? Strings.Details_ChartOnBattery : null));
    }

    /// <summary>When the station gave the answer on a card, and whether it has stopped giving it since.</summary>
    /// <returns>Nothing while there is no answer.</returns>
    public static string? DescribeAnswer(DateTimeOffset? receivedAt, bool isStale) =>
        receivedAt is { } answeredAt
            ? Phrases.Sentences(Phrases.AnsweredAt(answeredAt), isStale ? Strings.Details_CardStale : null)
            : null;

    public static string DescribeExport(string path) => Phrases.Format(Strings.Journal_Exported, path);

    public static string DescribeExportFailure(Exception failure) => Phrases.Format(Strings.Journal_ExportFailed, FailureReasons.OfFile(failure));

    // The highest reading is worth a word only where the point stands for several that differ.
    private static string DescribeLoad(ChartReading reading)
    {
        if (reading.Load is not { } load)
            return Phrases.Format(Strings.Details_ChartLoad, StationSections.NoValue);

        var average = Units.Format(load);

        return reading.LoadMax is { } max && Units.Format(max) != average
            ? Phrases.Format(Strings.Details_ChartLoadWithMax, average, Units.Format(max))
            : Phrases.Format(Strings.Details_ChartLoad, average);
    }

    // An idle battery has no direction to be told by, so it is named.
    private static string DescribeBattery(Power? power) => power is { } known
        ? Phrases.BatteryPower(known) ?? Phrases.Format(Strings.Details_ChartBatteryIdle, Units.Format(Power.Zero))
        : Phrases.Format(Strings.Details_ChartBatteryIdle, StationSections.NoValue);

    private static DateTimeOffset WeekStart(DateTimeOffset now) =>
        CalendarPeriods.WeekStart(now, TimeZoneInfo.Local, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

    private static OutageTotalsCard DescribeTotals(string title, OutageTotals totals) =>
        totals.Count == 0
            ? new OutageTotalsCard(title, Strings.Journal_None, Details: null)
            : new OutageTotalsCard(
                title,
                Phrases.Duration(totals.Duration, totals.IsDurationExact),
                Phrases.Readings(Phrases.Format(Strings.Journal_Count, totals.Count), Units.Format(totals.ConsumedEnergy)));

    private static OutageEntry Describe(Outage outage, int yearInView, DateTimeOffset now) => new(
        Phrases.Moment(outage.StartedAt, now, yearInView),
        outage.IsOngoing ? Strings.Journal_Ongoing : Phrases.Moment(outage.EndedAt, now, yearInView),
        OutagePhrases.Duration(outage),
        Units.Format(outage.ConsumedEnergy),
        OutagePhrases.ChargeChange(outage) ?? StationSections.NoValue,
        OutagePhrases.Unseen(outage),
        outage.IsOngoing);
}

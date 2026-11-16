using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class DetailsPresenterTests : LocalizedTests
{
    // A Thursday.
    private static readonly DateTimeOffset Now = Moments.Local(day: 12, hour: 12, minute: 42);
    private static readonly DateTimeOffset YesterdayEvening = Moments.Local(day: 11, hour: 19, minute: 13);
    private static readonly DateTimeOffset FirstOfTheMonth = Moments.Local(day: 1, hour: 0, minute: 0);
    private static readonly CalendarMonth ThisMonth = new(2026, 11);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeOutages_OutageSeenFromStartToEnd_TellsAllThatIsKnownOfIt(string language)
    {
        // Arrange
        Speak(language);
        var outage = OutageRecords.Finished(YesterdayEvening, TimeSpan.FromMinutes(135));

        // Act
        var entries = DetailsPresenter.DescribeOutages([outage], ThisMonth, Now);

        // Assert
        entries.Should().Equal(new OutageEntry(
            Started: Translated("yesterday 19:13"),
            Ended: Translated("yesterday 21:28"),
            Duration: Translated("2 h 15 min"),
            Energy: Translated("1.56 kWh"),
            Charge: "99% → 91%",
            Hint: null,
            IsOngoing: false));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeOutages_OutageGoingOn_HasNoEndYet(string language)
    {
        // Arrange
        Speak(language);
        var outage = OutageRecords.Finished(Now - TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(40)) with { IsOngoing = true, IsEndObserved = false };

        // Act
        var entry = DetailsPresenter.DescribeOutages([outage], ThisMonth, Now).Single();

        // Assert
        entry.Started.Should().Be("12:02");
        entry.Ended.Should().Be(Translated("ongoing"));
        entry.Duration.Should().Be(Translated("40 min"));
        entry.Hint.Should().BeNull();
        entry.IsOngoing.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, "It had begun before the app first saw it.")]
    [InlineData(true, false, "The app did not see it end, only when it was last going on.")]
    [InlineData(false, false, "It had begun before the app first saw it. The app did not see it end, only when it was last going on.")]
    public void DescribeOutages_OutageSeenInPart_SaysAtLeastAndWhatWasMissed(bool isStartObserved, bool isEndObserved, string hint)
    {
        // Arrange
        var outage = OutageRecords.Finished(YesterdayEvening, TimeSpan.FromMinutes(135)) with
        {
            IsStartObserved = isStartObserved,
            IsEndObserved = isEndObserved,
        };

        // Act
        var entry = DetailsPresenter.DescribeOutages([outage], ThisMonth, Now).Single();

        // Assert
        entry.Duration.Should().Be("at least 2 h 15 min");
        entry.Hint.Should().Be(hint);
    }

    [Fact]
    public void DescribeOutages_OutageGoingOnSinceBeforeTheAppWatched_MissesOnlyItsStart()
    {
        // Arrange
        var outage = OutageRecords.Finished(Now - TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(40)) with
        {
            IsOngoing = true,
            IsStartObserved = false,
            IsEndObserved = false,
        };

        // Act
        var entry = DetailsPresenter.DescribeOutages([outage], ThisMonth, Now).Single();

        // Assert
        entry.Duration.Should().Be("at least 40 min");
        entry.Hint.Should().Be("It had begun before the app first saw it.");
    }

    [Fact]
    public void DescribeOutages_ChargeNotOnRecord_ShowsADash()
    {
        // Arrange
        var outage = OutageRecords.Finished(YesterdayEvening, TimeSpan.FromMinutes(135)) with { StartSocPercent = null };

        // Act
        var entry = DetailsPresenter.DescribeOutages([outage], ThisMonth, Now).Single();

        // Assert
        entry.Charge.Should().Be(StationSections.NoValue);
    }

    [Fact]
    public void DescribeOutages_Entry_ReadsToAScreenReaderAsItsColumns()
    {
        // Arrange
        var outage = OutageRecords.Finished(YesterdayEvening, TimeSpan.FromMinutes(135));

        // Act
        var entry = DetailsPresenter.DescribeOutages([outage], ThisMonth, Now).Single();

        // Assert
        entry.ToString().Should().Be("yesterday 19:13 · yesterday 21:28 · 2 h 15 min · 1.56 kWh · 99% → 91%");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeOutages_OutageAcrossTheTurnOfTheYear_NamesTheYearOfADateOutsideTheMonthsOwn(string language)
    {
        // Arrange
        Speak(language);
        var outage = OutageRecords.Finished(Moments.LocalIn(12, 31, 23, 0, year: 2025), TimeSpan.FromHours(2));

        // Act
        var entry = DetailsPresenter.DescribeOutages([outage], new CalendarMonth(2026, 1), Now).Single();

        // Assert
        entry.Started.Should().Be(Translated("31 Dec 2025 23:00"));
        entry.Ended.Should().Be(Translated("1 Jan 01:00"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeMonthTotal_PastMonthWithAnOutageIntoTheNext_CountsOnlyThePartInside(string language)
    {
        // Arrange
        Speak(language);
        Outage[] outages =
        [
            OutageRecords.Finished(Moments.LocalIn(10, 31, 23, 0), TimeSpan.FromMinutes(90), wattHours: 600),
            OutageRecords.Finished(Moments.LocalIn(10, 10, 12, 0), TimeSpan.FromHours(1), wattHours: 300),
        ];

        // Act
        var total = DetailsPresenter.DescribeMonthTotal(outages, new CalendarMonth(2026, 10), Now, RecordedLoad.None);

        // Assert
        total.Should().Be(Translated("Month total: 2 h 00 min · Outages: 2 · 700 Wh"));
    }

    [Fact]
    public void DescribeMonthTotal_CurrentMonth_LeavesItToTheTotals()
    {
        // Arrange
        Outage[] outages = [OutageRecords.Finished(YesterdayEvening, TimeSpan.FromHours(1))];

        // Act
        var total = DetailsPresenter.DescribeMonthTotal(outages, ThisMonth, Now, RecordedLoad.None);

        // Assert
        total.Should().BeNull();
    }

    [Fact]
    public void DescribeMonthTotal_PastMonthWithoutOutages_SaysNothing()
    {
        // Act
        var total = DetailsPresenter.DescribeMonthTotal([], new CalendarMonth(2026, 10), Now, RecordedLoad.None);

        // Assert
        total.Should().BeNull();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeTotals_NoOutages_SaysSoForEveryPeriod(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var totals = DetailsPresenter.DescribeTotals([], Now, RecordedLoad.None);

        // Assert
        var none = Translated("no outages");
        totals.Should().Equal(
            new OutageTotalsCard(Translated("Today"), none, Details: null),
            new OutageTotalsCard(Translated("This week"), none, Details: null),
            new OutageTotalsCard(Translated("This month"), none, Details: null));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeTotals_Outages_CountTowardsTheCalendarPeriodsTheyFallInto(string language)
    {
        // Arrange
        Speak(language);
        Outage[] outages =
        [
            OutageRecords.Finished(Moments.Local(day: 12, hour: 9, minute: 42), TimeSpan.FromMinutes(45), wattHours: 208.2),
            OutageRecords.Finished(Moments.Local(day: 9, hour: 12, minute: 42), TimeSpan.FromMinutes(80), wattHours: 300),
            OutageRecords.Finished(Moments.Local(day: 3, hour: 18, minute: 0), TimeSpan.FromMinutes(60), wattHours: 400),
        ];

        // Act
        var totals = DetailsPresenter.DescribeTotals(outages, Now, RecordedLoad.None);

        // Assert
        totals.Select(card => (card.Headline, card.Details)).Should().Equal(
            (Translated("45 min"), Translated("Outages: 1 · 208.2 Wh")),
            (Translated("2 h 05 min"), Translated("Outages: 2 · 508.2 Wh")),
            (Translated("3 h 05 min"), Translated("Outages: 3 · 908.2 Wh")));
    }

    [Theory]
    [InlineData(RegionalFormatOfTheUnitedStates, "1 h 00 min")]
    [InlineData(RegionalFormatOfUkraine, "no outages")]
    public void DescribeTotals_OutageOnTheSundayBefore_IsOfThisWeekOnlyWhereWeeksBeginOnSunday(string regionalFormat, string thisWeek)
    {
        // Arrange
        Speak(English, regionalFormat);
        var outage = OutageRecords.Finished(Moments.Local(day: 8, hour: 10, minute: 0), TimeSpan.FromHours(1));

        // Act
        var totals = DetailsPresenter.DescribeTotals([outage], Now, RecordedLoad.None);

        // Assert
        totals[0].Headline.Should().Be("no outages");
        totals[1].Headline.Should().Be(thisWeek);
        totals[2].Headline.Should().Be("1 h 00 min");
    }

    [Fact]
    public void DescribeTotals_OutageSeenInPart_MakesTheTimeAtLeast()
    {
        // Arrange
        var outage = OutageRecords.Finished(Now - TimeSpan.FromHours(3), TimeSpan.FromMinutes(45)) with { IsStartObserved = false };

        // Act
        var today = DetailsPresenter.DescribeTotals([outage], Now, RecordedLoad.None)[0];

        // Assert
        today.Headline.Should().Be("at least 45 min");
        today.ToString().Should().Be("Today · at least 45 min · Outages: 1 · 1.56 kWh");
    }

    [Fact]
    public void DescribeTotals_OutageAcrossMidnight_CountsTowardsTodayWithThePartAfterIt()
    {
        // Arrange
        var outage = OutageRecords.Finished(Moments.Local(day: 11, hour: 23, minute: 0), TimeSpan.FromHours(2), wattHours: 600);

        // Act
        var totals = DetailsPresenter.DescribeTotals([outage], Now, RecordedLoad.None);

        // Assert
        totals[0].Headline.Should().Be("1 h 00 min");
        totals[0].Details.Should().Be("Outages: 1 · 300 Wh");
        totals[1].Headline.Should().Be("2 h 00 min");
        totals[1].Details.Should().Be("Outages: 1 · 600 Wh");
    }

    [Fact]
    public void DescribeTotals_OutageAcrossTheStartOfTheMonth_CountsTowardsTheMonthWithThePartInside()
    {
        // Arrange
        var thirdOfTheMonth = Moments.Local(day: 3, hour: 10, minute: 0);
        var outage = OutageRecords.Finished(FirstOfTheMonth - TimeSpan.FromHours(2), TimeSpan.FromHours(3), wattHours: 600);

        // Act
        var totals = DetailsPresenter.DescribeTotals([outage], thirdOfTheMonth, RecordedLoad.None);

        // Assert
        totals[0].Headline.Should().Be("no outages");
        totals[2].Headline.Should().Be("1 h 00 min");
        totals[2].Details.Should().Be("Outages: 1 · 200 Wh");
    }

    [Fact]
    public void DescribeTotals_OutageGoingOn_CountsUpToWhenItWasLastSeen()
    {
        // Arrange
        var outage = OutageRecords.Finished(Now - TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(39)) with { IsOngoing = true, IsEndObserved = false };

        // Act
        var today = DetailsPresenter.DescribeTotals([outage], Now, RecordedLoad.None)[0];

        // Assert
        today.Headline.Should().Be("39 min");
    }

    [Fact]
    public void TotalsFrom_WeekThatBeganWithinTheMonth_ReachesBackToTheFirstOfTheMonth()
    {
        DetailsPresenter.TotalsFrom(Now).Should().Be(FirstOfTheMonth);
    }

    [Fact]
    public void TotalsFrom_WeekThatBeganInTheMonthBefore_ReachesBackToItsStart()
    {
        // Arrange
        Speak(English, RegionalFormatOfUkraine);
        var sundayTheFirst = Moments.Local(day: 1, hour: 12, minute: 0);

        // Act
        var from = DetailsPresenter.TotalsFrom(sundayTheFirst);

        // Assert
        from.Should().Be(FirstOfTheMonth - TimeSpan.FromDays(6));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeChart_PointerOverAnOutage_ReadsTheValuesThere(string language)
    {
        // Arrange
        Speak(language);
        var chart = Chart(Sample(YesterdayEvening, loadWatts: 227, soc: 97.1, PowerSource.Battery, batteryWatts: -247));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: YesterdayEvening + TimeSpan.FromSeconds(10), Now);

        // Assert
        text.Should().Be(Translated("yesterday 19:13: load 227 W · charge 97.1% · discharging 247 W · on battery"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeChart_PointThatStandsForSeveralSamples_NamesTheHighestOfThem(string language)
    {
        // Arrange
        Speak(language);
        var chart = Chart(
            Sample(YesterdayEvening, loadWatts: 200, soc: 100, PowerSource.Mains),
            Sample(YesterdayEvening + PollInterval, loadWatts: 900, soc: 100, PowerSource.Mains),
            Sample(YesterdayEvening + 2 * PollInterval, loadWatts: 250, soc: 100, PowerSource.Mains));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: YesterdayEvening, Now);

        // Assert
        chart.Load.Should().ContainSingle().Which.Should().ContainSingle();
        text.Should().Be(Translated("yesterday 19:13: load 450 W (max 900 W) · charge 100% · battery 0 W"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeChart_PointerWhileTheBatteryCharges_SaysSoRatherThanGivingASign(string language)
    {
        // Arrange
        Speak(language);
        var chart = Chart(Sample(YesterdayEvening, loadWatts: 250, soc: 80, PowerSource.Mains, batteryWatts: 2250));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: YesterdayEvening, Now);

        // Assert
        text.Should().Be(Translated("yesterday 19:13: load 250 W · charge 80% · charging 2250 W"));
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(-0.4)]
    public void DescribeChart_BatteryPowerThatRoundsToNothing_IsABatteryAtRest(double batteryWatts)
    {
        // Arrange
        var chart = Chart(Sample(YesterdayEvening, loadWatts: 1172, soc: 100, PowerSource.Mains, batteryWatts));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: YesterdayEvening, Now);

        // Assert
        text.Should().Be("yesterday 19:13: load 1172 W · charge 100% · battery 0 W");
    }

    [Fact]
    public void DescribeChart_LineWithoutAValueThere_ShowsADash()
    {
        // Arrange
        var chart = Chart(new StationSample(YesterdayEvening, SocPercent: 97, BatteryPower: null, BackupOutputPower: null, PowerSource.Mains));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: YesterdayEvening, Now);

        // Assert
        text.Should().Be("yesterday 19:13: load — · charge 97% · battery —");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeChart_NoPointer_SumsUpTheLoad(string language)
    {
        // Arrange
        Speak(language);
        var chart = Chart(
            Sample(YesterdayEvening, loadWatts: 200, soc: 100, PowerSource.Mains),
            Sample(YesterdayEvening + PollInterval, loadWatts: 300, soc: 100, PowerSource.Mains));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: null, Now);

        // Assert
        text.Should().Be(Translated("Load on the backup output over 24 h: min 200 W · average 250 W · max 300 W."));
    }

    [Fact]
    public void DescribeChart_PointerWhereNothingIsKnown_FallsBackToTheSummary()
    {
        // Arrange
        var chart = Chart(Sample(YesterdayEvening, loadWatts: 200, soc: 100, PowerSource.Mains));

        // Act
        var text = DetailsPresenter.DescribeChart(chart, pointer: YesterdayEvening + TimeSpan.FromHours(6), Now);

        // Assert
        text.Should().Be(DetailsPresenter.DescribeChart(chart, pointer: null, Now));
    }

    private static StationSample Sample(DateTimeOffset at, double loadWatts, double soc, PowerSource source, double batteryWatts = 0) =>
        new(at, soc, new Power(batteryWatts), new Power(loadWatts), source);

    private static HistoryChart Chart(params StationSample[] samples) =>
        HistoryChart.Build(samples, Now - TimeSpan.FromHours(24), Now, columns: 800, longestBridgedGap: PollInterval * 4);
}

using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public sealed class JournalExportTests : LocalizedTests
{
    private const string LineBreak = "\r\n";

    private static readonly DateTimeOffset Evening = Moments.Local(day: 9, hour: 17, minute: 2);
    private static readonly DateTimeOffset Morning = Moments.Local(day: 12, hour: 9, minute: 42, second: 7);

    private readonly TemporaryDirectory _directory = new();

    public override void Dispose()
    {
        _directory.Dispose();
        base.Dispose();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Compose_Outages_GivesTitlesAndALineForEachInTheRegionalFormat(string language)
    {
        // Arrange
        Speak(language);
        Outage[] outages =
        [
            OutageRecords.Finished(Morning, TimeSpan.FromSeconds(2715), wattHours: 208.2) with { IsStartObserved = false },
            OutageRecords.Finished(Evening, TimeSpan.FromMinutes(135)),
        ];

        // Act
        var lines = LinesOf(JournalExport.Compose(Whole(outages)));

        // Assert
        lines.Should().Equal(
            Translated("Began,Ended,Duration,\"Energy, Wh\",\"Charge at the start, %\",\"Charge at the end, %\",Start not seen,End not seen,Cut by the month"),
            Translated("11/9/2026 17:02:00,11/9/2026 19:17:00,2:15:00,1560,99,91,,,"),
            Translated("11/12/2026 09:42:07,11/12/2026 10:27:22,0:45:15,208.2,99,91,yes,,"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Compose_OutageGoingOn_HasAWordForItsEndAndNoMarkAgainstIt(string language)
    {
        // Arrange
        Speak(language);
        var outage = OutageRecords.Finished(Evening, TimeSpan.FromMinutes(40)) with { IsOngoing = true, IsEndObserved = false };

        // Act
        var line = LinesOf(JournalExport.Compose(Whole(outage)))[1];

        // Assert
        line.Should().Be(Translated("11/9/2026 17:02:00,ongoing,0:40:00,1560,99,91,,,"));
    }

    [Fact]
    public void Compose_OutageWhoseEndWasNotSeen_MarksThat()
    {
        // Arrange
        var outage = OutageRecords.Finished(Evening, TimeSpan.FromMinutes(40)) with { IsEndObserved = false };

        // Act
        var line = LinesOf(JournalExport.Compose(Whole(outage)))[1];

        // Assert
        line.Should().Be("11/9/2026 17:02:00,11/9/2026 17:42:00,0:40:00,1560,99,91,,yes,");
    }

    [Fact]
    public void Compose_ChargeNotOnRecord_LeavesTheCellEmpty()
    {
        // Arrange
        var outage = OutageRecords.Finished(Evening, TimeSpan.FromMinutes(40)) with { StartSocPercent = null, EndSocPercent = 90.5 };

        // Act
        var line = LinesOf(JournalExport.Compose(Whole(outage)))[1];

        // Assert
        line.Should().Be("11/9/2026 17:02:00,11/9/2026 17:42:00,0:40:00,1560,,90.5,,,");
    }

    [Fact]
    public void Compose_OutageLongerThanADay_KeepsCountingHours()
    {
        // Arrange
        var outage = OutageRecords.Finished(Evening, TimeSpan.FromHours(26.5));

        // Act
        var line = LinesOf(JournalExport.Compose(Whole(outage)))[1];

        // Assert
        line.Should().Be("11/9/2026 17:02:00,11/10/2026 19:32:00,26:30:00,1560,99,91,,,");
    }

    [Fact]
    public void Compose_LanguageAndRegionalFormatThatDiffer_TakesTheTitlesFromOneAndTheRestFromTheOther()
    {
        // Arrange
        Speak(English);
        var inEnglish = LinesOf(JournalExport.Compose(Whole(OutageRecords.Finished(Evening, TimeSpan.FromMinutes(135)))));
        Speak(Ukrainian, RegionalFormatOfTheUnitedStates);

        // Act
        var lines = LinesOf(JournalExport.Compose(Whole(OutageRecords.Finished(Evening, TimeSpan.FromMinutes(135)))));

        // Assert
        lines[0].Should().NotBe(inEnglish[0]);
        lines[0].Split(',').Should().HaveCountGreaterThan(8, "a title with a comma in it is quoted rather than cut in two");
        lines[0].Should().Contain("\"");
        lines[1].Should().Be(inEnglish[1]);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Compose_OutageFromTheMonthBefore_KeepsThePartInsideAndMarksTheCut(string language)
    {
        // Arrange
        Speak(language);
        var outage = OutageRecords.Finished(Moments.LocalIn(10, 31, 23, 0), TimeSpan.FromMinutes(90), wattHours: 600) with { IsStartObserved = false };
        var november = new CalendarMonth(2026, 11);

        // Act
        var line = LinesOf(JournalExport.Compose(PartsIn(november, outage)))[1];

        // Assert
        line.Should().Be(Translated("11/1/2026 00:00:00,11/1/2026 00:30:00,0:30:00,200,,91,,,began earlier"));
    }

    [Fact]
    public void Compose_OutageIntoTheMonthAfter_KeepsThePartInsideAndMarksTheCut()
    {
        // Arrange
        var outage = OutageRecords.Finished(Moments.LocalIn(10, 31, 23, 0), TimeSpan.FromMinutes(90), wattHours: 600) with { IsOngoing = true };

        // Act
        var line = LinesOf(JournalExport.Compose(PartsIn(new CalendarMonth(2026, 10), outage)))[1];

        // Assert
        line.Should().Be("10/31/2026 23:00:00,11/1/2026 00:00:00,1:00:00,400,99,,,,went on later");
    }

    [Fact]
    public void Compose_OutageThroughTheWholeMonth_MarksBothCuts()
    {
        // Arrange
        var outage = OutageRecords.Finished(Moments.LocalIn(10, 31, 23, 0), TimeSpan.FromDays(31));

        // Act
        var line = LinesOf(JournalExport.Compose(PartsIn(new CalendarMonth(2026, 11), outage)))[1];

        // Assert
        line.Should().StartWith("11/1/2026 00:00:00,12/1/2026 00:00:00,");
        line.Should().EndWith(",,,,,\"began earlier, went on later\"");
    }

    [Fact]
    public void Compose_NoOutages_GivesTheTitlesAlone()
    {
        // Act
        var text = JournalExport.Compose([]);

        // Assert
        LinesOf(text).Should().ContainSingle();
        text.Should().EndWith(LineBreak);
    }

    [Fact]
    public void Save_WritesUtf8WithTheMarkExcelNeeds()
    {
        // Arrange
        Speak(Ukrainian);
        Outage[] outages = [OutageRecords.Finished(Evening, TimeSpan.FromMinutes(135))];
        var path = Path.Combine(_directory.Path, "outages.csv");

        // Act
        JournalExport.Save(path, Whole(outages));

        // Assert
        var written = File.ReadAllBytes(path);
        written.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        File.ReadAllText(path).Should().Be(JournalExport.Compose(Whole(outages)));
    }

    [Fact]
    public void SuggestFileName_NamesTheMonth()
    {
        JournalExport.SuggestFileName(new CalendarMonth(2026, 3)).Should().Be("marstek-outages-2026-03.csv");
    }

    private static OutagePart[] PartsIn(CalendarMonth month, params Outage[] outages) =>
        [.. OutagePart.AllWithin(outages, month.StartsAt(TimeZoneInfo.Local), month.EndsAt(TimeZoneInfo.Local), RecordedLoad.None)];

    private static OutagePart[] Whole(params Outage[] outages) =>
        [.. OutagePart.AllWithin(outages, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, RecordedLoad.None)];

    private static string[] LinesOf(string text) => text.Split(LineBreak, StringSplitOptions.RemoveEmptyEntries);
}

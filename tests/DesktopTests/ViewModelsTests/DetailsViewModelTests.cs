using FluentAssertions;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewModelsTests;

public class DetailsViewModelTests : LocalizedTests
{
    private static readonly Outage InOctober = Finished(Moments.LocalIn(10, 10, 9, 0), TimeSpan.FromHours(1));
    private static readonly Outage InNovember = Finished(Moments.Local(12, 18, 0), TimeSpan.FromMinutes(45));

    // Half a minute before the month turns over.
    private static readonly DateTimeOffset EndOfNovember = Moments.Local(30, 23, 59, 30);

    [Fact]
    public void Open_StationAnswering_GroupsTheCardsByWhatTheyAreAbout() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0));
        station.Answer(at => StationReadings.HoldingFull(at));

        // Act
        using var details = Open(station);

        // Assert
        string[][] groups =
        [
            [MarstekMethods.EnergySystemGetStatus, MarstekMethods.BatteryGetStatus, MarstekMethods.EnergySystemGetMode],
            [MarstekMethods.GetDevice, MarstekMethods.WifiGetStatus, MarstekMethods.BluetoothGetStatus],
            [MarstekMethods.EnergyMeterGetStatus, MarstekMethods.PvGetStatus],
        ];
        details.CardGroups.Select(group => group.Select(card => card.Method)).Should().BeEquivalentTo(
            groups,
            options => options.WithStrictOrdering());
        details.CardGroups[0][0].Rows.Should().NotBeEmpty();
    });

    [Fact]
    public void Open_JournalReachingBackAMonth_ShowsTheCurrentMonthAndOffersTheOneBefore() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InOctober, InNovember]);

        // Act
        using var details = Open(station);

        // Assert
        details.Journal.MonthTitle.Should().Be("November 2026");
        details.Journal.Outages.Should().ContainSingle().Which.Duration.Should().Be("45 min");
        details.Journal.ExportFileName.Should().Be(JournalExport.SuggestFileName(new CalendarMonth(2026, 11)));
        details.Journal.ShowPreviousMonthCommand.CanExecute(null).Should().BeTrue();
        details.Journal.ShowNextMonthCommand.CanExecute(null).Should().BeFalse();
    });

    [Fact]
    public void ShowPreviousMonth_ListsThatMonthUntilTheWayBack() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InOctober, InNovember]);
        using var details = Open(station);

        // Act
        details.Journal.ShowPreviousMonthCommand.Execute(null);
        var october = (details.Journal.MonthTitle, details.Journal.Outages, details.Journal.ExportFileName);
        var isFirstMonth = !details.Journal.ShowPreviousMonthCommand.CanExecute(null);
        details.Journal.ShowNextMonthCommand.Execute(null);

        // Assert
        october.MonthTitle.Should().Be("October 2026");
        october.Outages.Should().ContainSingle().Which.Duration.Should().Be("1 h 00 min");
        october.ExportFileName.Should().Be(JournalExport.SuggestFileName(new CalendarMonth(2026, 10)));
        isFirstMonth.Should().BeTrue();
        details.Journal.MonthTitle.Should().Be("November 2026");
        details.Journal.ShowNextMonthCommand.CanExecute(null).Should().BeFalse();
    });

    [Fact]
    public void ShowPreviousMonth_GivesTheTotalOfThatMonthWhichTheCurrentOneLeavesToTheCards() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InOctober, InNovember]);
        using var details = Open(station);
        var current = details.Journal.MonthTotal;

        // Act
        details.Journal.ShowPreviousMonthCommand.Execute(null);

        // Assert
        current.Should().BeNull();
        details.Journal.MonthTotal.Should().Be("Month total: 1 h 00 min · Outages: 1 · 250 Wh");
    });

    [Fact]
    public void Refresh_MonthTurnsOverWhileTheCurrentOneIsShown_GoesOnShowingTheCurrentOne() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(EndOfNovember, [InOctober, InNovember]);
        using var details = Open(station);

        // Act
        station.AnswerAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        details.Journal.MonthTitle.Should().Be("December 2026");
        details.Journal.Outages.Should().BeEmpty();
        details.Journal.ShowPreviousMonthCommand.CanExecute(null).Should().BeTrue();
        details.Journal.ShowNextMonthCommand.CanExecute(null).Should().BeFalse();
    });

    [Fact]
    public void Refresh_MonthTurnsOverWhileAnEarlierOneIsShown_StaysOnIt() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(EndOfNovember, [InOctober, InNovember]);
        using var details = Open(station);
        details.Journal.ShowPreviousMonthCommand.Execute(null);

        // Act
        station.AnswerAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        details.Journal.MonthTitle.Should().Be("October 2026");
        details.Journal.ShowNextMonthCommand.CanExecute(null).Should().BeTrue();
    });

    [Fact]
    public void Refresh_NewAnswers_AreAddedToTheChart() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0));
        using var details = Open(station);
        var empty = details.Charts.Chart;

        // Act
        station.Answer(at => StationReadings.HoldingFull(at, loadWatts: 200));
        station.AnswerAfterInterval(at => StationReadings.HoldingFull(at, loadWatts: 300));

        // Assert
        empty.Should().BeOfType<HistoryChart>().Which.Summary.Should().BeNull();
        details.Charts.Chart.Should().BeOfType<HistoryChart>()
            .Which.Summary.Should().Be(new LoadSummary(new Power(200), new Power(250), new Power(300)));
    });

    [Fact]
    public void Refresh_OutageConfirmedByTheSecondAnswer_ChartDatesItByTheFirst() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0));
        using var details = Open(station);
        station.Answer(at => StationReadings.Charging(at, soc: 80));

        // Act
        station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 79));
        var firstOnBattery = station.Now;
        station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 79));

        // Assert
        details.Charts.Chart.Should().BeOfType<HistoryChart>()
            .Which.OnBattery.Should().ContainSingle().Which.From.Should().Be(firstOnBattery);
    });

    [Fact]
    public void RecordsDeleted_EarlierMonthShown_ListsItAnewThoughTheLatestOutageStays() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InOctober, InNovember]);
        using var details = Open(station);
        details.Journal.ShowPreviousMonthCommand.Execute(null);
        var before = details.Journal.Outages;

        // Act
        station.Pipeline.DeleteBefore(Moments.Local(1, 0, 0), RecordKinds.Outages);

        // Assert
        before.Should().ContainSingle();
        details.Journal.MonthTitle.Should().Be("October 2026");
        details.Journal.Outages.Should().BeEmpty();
        details.Journal.ShowPreviousMonthCommand.CanExecute(null).Should().BeFalse();
    });

    [Fact]
    public void RecordsDeleted_SamplesGone_DrawsTheChartWithoutThem() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0));
        using var details = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at, loadWatts: 200));
        station.AnswerAfterInterval(at => StationReadings.HoldingFull(at, loadWatts: 300));

        // Act
        station.Pipeline.DeleteBefore(station.Now, RecordKinds.Samples);
        var afterTheFirstWasDeleted = details.Charts.Chart;
        station.Pipeline.DeleteBefore(DateTimeOffset.MaxValue, RecordKinds.Samples);

        // Assert
        afterTheFirstWasDeleted.Should().BeOfType<HistoryChart>()
            .Which.Summary.Should().Be(new LoadSummary(new Power(300), new Power(300), new Power(300)));
        details.Charts.Chart.Should().BeOfType<HistoryChart>().Which.Summary.Should().BeNull();
    });

    [Fact]
    public void JournalExport_FolderThere_SavesTheMonthShownAndSaysWhere() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InOctober, InNovember]);
        using var details = Open(station);
        var path = station.Storage.Resolve("journal.csv");

        // Act
        details.Journal.Export(path);

        // Assert
        File.Exists(path).Should().BeTrue();
        details.Journal.ExportStatus.Should().Be($"Saved to {path}.");
    });

    [Fact]
    public void JournalExport_FolderMissing_SaysThatNothingWasSaved() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0));
        using var details = Open(station);

        // Act
        details.Journal.Export(station.Storage.Resolve(Path.Combine("missing", "journal.csv")));

        // Assert
        details.Journal.ExportStatus.Should().Be("The file was not saved: the file is in use by another program or cannot be reached.");
    });

    [Fact]
    public void ShowPreviousMonth_AfterAnExport_DropsTheWordOnIt() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InOctober, InNovember]);
        using var details = Open(station);
        details.Journal.Export(station.Storage.Resolve("journal.csv"));

        // Act
        details.Journal.ShowPreviousMonthCommand.Execute(null);

        // Assert
        details.Journal.ExportStatus.Should().BeNull();
    });

    [Fact]
    public void LanguageChanged_WordsTheJournalAnewAndDropsTheWordOnAnExport() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0), [InNovember]);
        using var details = Open(station);
        details.Journal.Export(station.Storage.Resolve("journal.csv"));

        // Act
        Speak(Ukrainian);

        // Assert
        details.Journal.MonthTitle.Should().Be(Translated("November 2026"));
        details.Journal.ExportStatus.Should().BeNull();
    });

    [Fact]
    public void Dispose_WindowClosed_NoLongerFollowsThePipeline() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Moments.Local(14, 12, 0));
        var details = Open(station);
        var chart = details.Charts.Chart;

        // Act
        details.Dispose();
        station.AnswerAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        details.Charts.Chart.Should().BeSameAs(chart);
    });

    private static DetailsViewModel Open(StationHarness station) => new(
        station.Settings,
        station.Monitor,
        station.Pipeline,
        station.Journal,
        station.History,
        Localizer.Instance,
        Options.Create(new HistorySettings()),
        Options.Create(station.Polling),
        station.Time,
        NullLogger<DetailsViewModel>.Instance);

    private static Outage Finished(DateTimeOffset startedAt, TimeSpan duration) =>
        OutageRecords.Finished(startedAt, duration, wattHours: 250 * duration.TotalHours, startSoc: 100, endSoc: 95);
}

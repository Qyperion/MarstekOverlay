using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewModelsTests;

public sealed class StoredDataViewModelTests : LocalizedTests
{
    private static readonly DateTimeOffset Start = Moments.Local(14, 12, 0);
    private static readonly Outage MonthAgo = Finished(Moments.LocalIn(10, 10, 9, 0));
    private static readonly Outage TwoDaysAgo = Finished(Moments.Local(12, 18, 0));

    private readonly StationHarness _station = new(Start, [MonthAgo, TwoDaysAgo]);
    private readonly DiagnosticLog _log;
    private readonly string _logPath;

    public StoredDataViewModelTests()
    {
        var options = new LoggerOptions();

        _log = new DiagnosticLog(_station.Storage, Options.Create(options));
        _logPath = _station.Storage.Resolve(options.LogFilePath);

        _station.Answer(at => StationReadings.HoldingFull(at));
        _station.AnswerAfterInterval(at => StationReadings.HoldingFull(at));
    }

    private IReadOnlyList<Outage> Journal => _station.Journal.Since(DateTimeOffset.MinValue);

    public override void Dispose()
    {
        _log.Dispose();
        _station.Dispose();
        base.Dispose();
    }

    [Fact]
    public void CanDelete_NothingTicked_IsFalseUntilSomethingIs()
    {
        // Arrange
        var storedData = Open();
        var before = storedData.CanDelete;

        // Act
        storedData.DeletesLog = true;

        // Assert
        before.Should().BeFalse();
        storedData.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_LogTicked_KeepsTheButtonOffUntilTheWordOnTheDeletionIsThere()
    {
        // Arrange
        File.WriteAllLines(_logPath, [LogEntries.Written(_station.Now - TimeSpan.FromDays(40), "Alert raised")]);
        var storedData = Open();
        storedData.DeletesSamples = true;
        storedData.DeletesLog = true;
        var seen = new List<string>();
        storedData.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(StoredDataViewModel.CanDelete))
                seen.Add($"CanDelete {storedData.CanDelete}");
            else if (change.PropertyName == nameof(StoredDataViewModel.Status) && storedData.Status is not null)
                seen.Add("Status");
        };

        // Act
        var deleting = storedData.DeleteAsync();
        var canDeleteWhileDeleting = storedData.CanDelete;
        await deleting;

        // Assert
        canDeleteWhileDeleting.Should().BeFalse();
        seen.Should().Equal("CanDelete False", "Status", "CanDelete True");
        storedData.IsDeleting.Should().BeFalse();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public async Task Delete_EverythingTickedAndNothingKept_DeletesItAllAndSaysHowMuch(string language)
    {
        // Arrange
        Speak(language);
        File.WriteAllLines(_logPath, [LogEntries.Written(_station.Now, "Alert raised")]);
        var storedData = Open();
        storedData.KeptPeriods[0].IsSelected = true;
        storedData.DeletesSamples = true;
        storedData.DeletesOutages = true;
        storedData.DeletesLog = true;

        // Act
        await storedData.DeleteAsync();

        // Assert
        storedData.Status.Should().Be(new StatusMessage(
            Translated("Measurements deleted: 2. Outages deleted: 2. The diagnostic log is cleared."),
            IsError: false));
        _station.History.Since(DateTimeOffset.MinValue).Should().BeEmpty();
        Journal.Should().BeEmpty();
        File.Exists(_logPath).Should().BeFalse();
    }

    [Fact]
    public void Open_NothingChosenYet_KeepsTheLongestPeriodAndAsksAboutWhatIsTicked()
    {
        // Arrange
        var storedData = Open();

        // Act
        storedData.DeletesOutages = true;

        // Assert
        storedData.KeptPeriods.Single(period => period.IsSelected).Name.Should().Be("the last month");
        storedData.Confirmation.Split(Environment.NewLine).Should().Equal(
            "Delete this, keeping the last month?",
            string.Empty,
            "• Outage journal",
            string.Empty,
            "It cannot be restored.");
    }

    [Fact]
    public async Task Delete_SamplesAndOutagesTicked_HasTheRecordsReadAgainOnce()
    {
        // Arrange
        var storedData = Open();
        storedData.KeptPeriods[0].IsSelected = true;
        storedData.DeletesSamples = true;
        storedData.DeletesOutages = true;
        var readAgain = 0;
        _station.Pipeline.RecordsDeleted += () => readAgain++;

        // Act
        await storedData.DeleteAsync();

        // Assert
        readAgain.Should().Be(1);
    }

    [Fact]
    public async Task Delete_TheLastWeekKept_LeavesWhatIsNewerAndWhatIsNotTicked()
    {
        // Arrange
        string[] recent = [LogEntries.Written(_station.Now - TimeSpan.FromDays(2), "Alert raised")];
        File.WriteAllLines(_logPath, [LogEntries.Written(_station.Now - TimeSpan.FromDays(8), "Alert raised"), .. recent]);
        var storedData = Open();
        storedData.KeptPeriods.Single(period => period.Name == "the last week").IsSelected = true;
        storedData.DeletesOutages = true;
        storedData.DeletesLog = true;

        // Act
        await storedData.DeleteAsync();

        // Assert
        storedData.Status?.Text.Should().Be("Outages deleted: 1. The diagnostic log is cleared.");
        Journal.Should().Equal(TwoDaysAgo with { Id = 2 });
        _station.History.Since(DateTimeOffset.MinValue).Should().HaveCount(2);
        File.ReadAllLines(_logPath).Should().Equal(recent);
    }

    [Fact]
    public async Task Delete_LogHeldBySomethingElse_SaysSoAndStillDeletesTheRest()
    {
        // Arrange
        File.WriteAllLines(_logPath, [LogEntries.Written(_station.Now, "Alert raised")]);
        using var holder = File.Open(_logPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var storedData = Open();
        storedData.KeptPeriods[0].IsSelected = true;
        storedData.DeletesOutages = true;
        storedData.DeletesLog = true;

        // Act
        await storedData.DeleteAsync();

        // Assert
        storedData.Status?.Text.Should().Be(
            "Outages deleted: 2. The diagnostic log was not cleared: the file is in use by another program or cannot be reached.");
        storedData.Status?.IsError.Should().BeTrue();
        Journal.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshTexts_LanguageSwitched_RenamesTheChoicesAndDropsTheWordOnADeletion()
    {
        // Arrange
        var storedData = Open();
        storedData.DeletesSamples = true;
        await storedData.DeleteAsync();
        var renamed = new List<string?>();
        foreach (var period in storedData.KeptPeriods)
            period.PropertyChanged += (_, change) => renamed.Add(change.PropertyName);

        // Act
        Speak(Ukrainian);
        storedData.RefreshTexts();

        // Assert
        storedData.KeptPeriods.Select(period => period.Name).Should().Equal(
            Translated("nothing"),
            Translated("the last day"),
            Translated("the last week"),
            Translated("the last month"));
        renamed.Should().HaveCount(4).And.OnlyContain(property => property == nameof(ChoiceOption<KeptPeriod>.Name));
        storedData.Status.Should().BeNull();
    }

    private StoredDataViewModel Open() =>
        new(_station.Pipeline, _log, _station.Time, NullLogger<StoredDataViewModel>.Instance);

    private static Outage Finished(DateTimeOffset startedAt) =>
        OutageRecords.Finished(startedAt, TimeSpan.FromMinutes(45), wattHours: 180, startSoc: 100, endSoc: 96);
}

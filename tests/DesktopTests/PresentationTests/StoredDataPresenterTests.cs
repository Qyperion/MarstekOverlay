using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class StoredDataPresenterTests : LocalizedTests
{
    private static readonly IOException Failure = new("The process cannot access the file because it is being used by another process.");

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void DescribeDeletion_LogClearedOnlyInPart_SaysSoAsAnError(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var status = StoredDataPresenter.DescribeDeletion(new DeletedRecords(Samples: 1, Outages: null), new LogCleanup(Failure, IsPartial: true));

        // Assert
        status.Should().Be(new StatusMessage(
            Translated("Measurements deleted: 1. The diagnostic log is cleared only in part: the file is in use by another program or cannot be reached."),
            IsError: true));
    }

    [Fact]
    public void DescribeDeletion_LogNotClearedAtAll_SaysItWasNot()
    {
        // Act
        var status = StoredDataPresenter.DescribeDeletion(new DeletedRecords(Samples: null, Outages: 3), new LogCleanup(Failure, IsPartial: false));

        // Assert
        status.Should().Be(new StatusMessage(
            "Outages deleted: 3. The diagnostic log was not cleared: the file is in use by another program or cannot be reached.",
            IsError: true));
    }

    [Fact]
    public void DescribeDeletion_LogLeftAlone_SaysNothingOfIt()
    {
        // Act
        var status = StoredDataPresenter.DescribeDeletion(new DeletedRecords(Samples: 2, Outages: 0), log: null);

        // Assert
        status.Should().Be(new StatusMessage("Measurements deleted: 2. Outages deleted: 0.", IsError: false));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void AskToDelete_PartOfThePeriodKept_NamesWhatGoesAndWhatStays(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var question = StoredDataPresenter.AskToDelete(samples: true, outages: false, log: true, KeptPeriod.Week);

        // Assert
        question.Split(Environment.NewLine).Should().Equal(
            Translated("Delete this, keeping the last week?"),
            string.Empty,
            "• " + Translated("Measurements for the charts"),
            "• " + Translated("Diagnostic log"),
            string.Empty,
            Translated("It cannot be restored."));
    }

    [Fact]
    public void AskToDelete_NothingKept_AsksToDeleteAll()
    {
        // Act
        var question = StoredDataPresenter.AskToDelete(samples: false, outages: true, log: false, KeptPeriod.Nothing);

        // Assert
        question.Split(Environment.NewLine).Should().Equal(
            "Delete all of this?",
            string.Empty,
            "• Outage journal",
            string.Empty,
            "It cannot be restored.");
    }
}

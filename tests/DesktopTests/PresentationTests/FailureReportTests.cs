using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class FailureReportTests : LocalizedTests
{
    private const string LogPath = @"C:\Users\Owner\AppData\Roaming\MarstekOverlay\diagnostics.log";

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_FileLogOn_SaysTheAppClosesWhatWentWrongAndWhereTheLogIs(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = FailureReport.Describe(new InvalidOperationException("Sequence contains no elements"), LogPath);

        // Assert
        text.Should().Be(string.Join(
            Environment.NewLine,
            Translated("Marstek Overlay has run into an error it cannot recover from and will close."),
            "Sequence contains no elements",
            Translated($"Details are in the log: {LogPath}")));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_FileLogOff_LeavesTheLogOut(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = FailureReport.Describe(new InvalidOperationException("Sequence contains no elements"), logPath: null);

        // Assert
        text.Should().Be(string.Join(
            Environment.NewLine,
            Translated("Marstek Overlay has run into an error it cannot recover from and will close."),
            "Sequence contains no elements"));
    }
}

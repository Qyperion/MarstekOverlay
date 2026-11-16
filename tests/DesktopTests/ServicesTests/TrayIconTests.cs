using FluentAssertions;

using MarstekOverlay.Desktop.Services;
using MarstekOverlay.DesktopTests.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public class TrayIconTests : LocalizedTests
{
    private const string AppTitle = "Marstek Overlay";
    private const int LongestTooltip = 127;

    [Fact]
    public void Fit_ShortSummary_GoesUnderTheNameOfTheApp()
    {
        // Act
        var tooltip = TrayIcon.Fit(["On mains · 100%", "Backup output: 575 W", "Would last ≈ 14 h 30 min"]);

        // Assert
        tooltip.Should().Be("Marstek Overlay\nOn mains · 100%\nBackup output: 575 W\nWould last ≈ 14 h 30 min");
    }

    [Fact]
    public void Fit_NoSummary_IsTheNameOfTheApp()
    {
        TrayIcon.Fit([]).Should().Be(AppTitle);
    }

    [Fact]
    public void Fit_SummaryThatExactlyFills_IsKeptWhole()
    {
        // Arrange
        var line = Line(LongestTooltip - AppTitle.Length - 1);

        // Act
        var tooltip = TrayIcon.Fit([line]);

        // Assert
        tooltip.Should().Be(AppTitle + "\n" + line);
        tooltip.Should().HaveLength(LongestTooltip);
    }

    [Fact]
    public void Fit_SummaryTooLongWithTheName_DropsTheNameFirst()
    {
        // Arrange
        string[] summary = [Line(60), Line(60)];

        // Act
        var tooltip = TrayIcon.Fit(summary);

        // Assert
        tooltip.Should().Be(summary[0] + "\n" + summary[1]);
    }

    [Fact]
    public void Fit_SummaryTooLongOnItsOwn_DropsItsLastLines()
    {
        // Arrange
        string[] summary = [Line(60), Line(60), Line(60)];

        // Act
        var tooltip = TrayIcon.Fit(summary);

        // Assert
        tooltip.Should().Be(summary[0] + "\n" + summary[1]);
    }

    [Fact]
    public void Fit_OnlyTheFirstLineFits_IsThatLineWhole()
    {
        // Arrange
        string[] summary = [Line(100), Line(100)];

        // Act
        var tooltip = TrayIcon.Fit(summary);

        // Assert
        tooltip.Should().Be(summary[0]);
    }

    [Fact]
    public void Fit_FirstLineTooLongOnItsOwn_IsCutShortWithAnEllipsis()
    {
        // Arrange
        var line = Line(200);

        // Act
        var tooltip = TrayIcon.Fit([line, "Backup output: 575 W"]);

        // Assert
        tooltip.Should().Be(line[..(LongestTooltip - 1)] + "…");
        tooltip.Should().HaveLength(LongestTooltip);
    }

    private static string Line(int length) => new('x', length);
}

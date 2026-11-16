using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Settings;

using Xunit;

namespace MarstekOverlay.DesktopTests.SettingsTests;

public class OverlaySettingsTests
{
    private readonly OverlaySettings _settings = new();

    [Fact]
    public void Shows_NothingChosen_ShowsTheRowsThatAreOnByDefault()
    {
        // Act
        var shown = Enum.GetValues<OverlayRowKind>().Where(_settings.Shows);

        // Assert
        shown.Should().Equal(
            OverlayRowKind.Forecast,
            OverlayRowKind.Battery,
            OverlayRowKind.Grid,
            OverlayRowKind.BackupOutput,
            OverlayRowKind.Solar);
    }

    [Fact]
    public void Modes_EachSwitch_LandsInItsOwnPlace()
    {
        // Arrange
        _settings.IsTopmost = true;
        _settings.IsClickThrough = true;

        // Act
        var modes = _settings.Modes();

        // Assert
        modes.Should().Be(new PanelModes(IsTopmost: true, IsCompact: false, IsClickThrough: true));
    }

    [Fact]
    public void Show_ChoiceAgainstTheDefault_IsRemembered()
    {
        // Act
        _settings.Show(OverlayRowKind.Solar, isShown: false);
        _settings.Show(OverlayRowKind.WifiSignal, isShown: true);

        // Assert
        _settings.HiddenRows.Should().Equal(OverlayRowKind.Solar);
        _settings.ShownRows.Should().Equal(OverlayRowKind.WifiSignal);
        _settings.Shows(OverlayRowKind.Solar).Should().BeFalse();
        _settings.Shows(OverlayRowKind.WifiSignal).Should().BeTrue();
    }

    [Fact]
    public void Show_ChoiceBackToTheDefault_LeavesNothingBehind()
    {
        // Arrange
        _settings.Show(OverlayRowKind.Solar, isShown: false);
        _settings.Show(OverlayRowKind.WifiSignal, isShown: true);

        // Act
        _settings.Show(OverlayRowKind.Solar, isShown: true);
        _settings.Show(OverlayRowKind.WifiSignal, isShown: false);

        // Assert
        _settings.HiddenRows.Should().BeEmpty();
        _settings.ShownRows.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    [InlineData(23, 12)]
    [InlineData(1000, 24)]
    public void ChartPeriod_HoursFromAFileEditedByHand_AreTheOfferedPeriodTheSettingsWindowMarks(int savedHours, int usedHours)
    {
        // Arrange
        _settings.ChartHours = savedHours;

        // Act
        var period = _settings.ChartPeriod();

        // Assert
        period.Should().Be(TimeSpan.FromHours(usedHours));
    }

    [Theory]
    [InlineData(10, 0.75)]
    [InlineData(150, 1.5)]
    [InlineData(900, 2)]
    public void Scale_StaysWithinItsRange(int savedPercent, double scale)
    {
        // Arrange
        _settings.ScalePercent = savedPercent;

        // Act & Assert
        _settings.Scale().Should().BeApproximately(scale, 1e-9);
    }

    [Theory]
    [InlineData(0, 0.2)]
    [InlineData(80, 0.8)]
    [InlineData(300, 1)]
    public void Opacity_StaysWithinItsRange(int savedPercent, double opacity)
    {
        // Arrange
        _settings.OpacityPercent = savedPercent;

        // Act & Assert
        _settings.Opacity().Should().BeApproximately(opacity, 1e-9);
    }
}

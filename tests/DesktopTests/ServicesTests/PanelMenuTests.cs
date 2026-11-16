using FluentAssertions;

using MarstekOverlay.Desktop.Services;
using MarstekOverlay.DesktopTests.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public sealed class PanelMenuTests : LocalizedTests
{
    private readonly StationHarness _station = new(Moments.Local(14, 12, 0));
    private readonly RecordingShell _shell = new();

    public override void Dispose()
    {
        _station.Dispose();
        base.Dispose();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Groups_SwitchesOfThePanel_AreTickedAsTheSettingsStand(string language)
    {
        // Arrange
        Speak(language);
        _station.Settings.Change(current => current.Overlay.IsCompact = true);

        // Act
        var groups = Menu().Groups();

        // Assert
        groups.Select(group => group.Select(entry => (entry.Text, entry.IsChecked))).Should().BeEquivalentTo(
            new[]
            {
                new[] { (Translated("Refresh now"), (bool?)null) },
                [
                    (Translated("Compact view"), true),
                    (Translated("Always on top"), false),
                    (Translated("Let clicks through"), false),
                ],
                [(Translated("Details…"), null), (Translated("Settings…"), null)],
                [(Translated("Exit"), null)],
            },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Choose_Switch_TurnsItOverAndKeepsTheRestAsTheyWere()
    {
        // Arrange
        var menu = Menu();

        // Act
        Entry(menu, "Let clicks through").Choose();
        Entry(menu, "Compact view").Choose();
        Entry(menu, "Compact view").Choose();

        // Assert
        var overlay = _station.Settings.Current.Overlay;
        (overlay.IsClickThrough, overlay.IsCompact, overlay.IsTopmost).Should().Be((true, false, false));
        Entry(menu, "Let clicks through").IsChecked.Should().BeTrue();
    }

    [Fact]
    public void Choose_WindowsAndExit_ReachTheShell()
    {
        // Arrange
        var menu = Menu();

        // Act
        Entry(menu, "Details…").Choose();
        Entry(menu, "Settings…").Choose();
        Entry(menu, "Exit").Choose();

        // Assert
        _shell.Calls.Should().Equal(nameof(IShell.ShowDetails), nameof(IShell.ShowSettings), nameof(IShell.Exit));
    }

    private static MenuEntry Entry(PanelMenu menu, string text) =>
        menu.Groups().SelectMany(group => group).Single(entry => entry.Text == text);

    private PanelMenu Menu() => new(_shell, _station.Settings, _station.Monitor);
}

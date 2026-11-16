using FluentAssertions;

using MarstekOverlay.Desktop.Services;
using MarstekOverlay.DesktopTests.TestSupport;

using System.Windows.Forms;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public sealed class TrayMenuTests : LocalizedTests
{
    private const string Separator = "—";

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
    public void Constructor_AnyState_OffersWhatThePanelMenuDoesLedByShowingOrHidingThePanel(string language)
    {
        // Arrange
        Speak(language);
        _station.Settings.Change(current => current.Overlay.IsCompact = true);

        // Act
        using var menu = Menu();

        // Assert
        Items(menu).Should().Equal(
            (Translated("Hide panel"), false),
            (Translated("Refresh now"), false),
            (Separator, false),
            (Translated("Compact view"), true),
            (Translated("Always on top"), false),
            (Translated("Let clicks through"), false),
            (Separator, false),
            (Translated("Details…"), false),
            (Translated("Settings…"), false),
            (Separator, false),
            (Translated("Exit"), false));
    }

    [Fact]
    public void Reword_SettingsChangedSinceBuilt_TicksTheSwitchesAsTheyNowStand()
    {
        // Arrange
        using var menu = Menu();

        // Act
        _station.Settings.Change(current => current.Overlay.IsTopmost = true);
        menu.Reword();

        // Assert
        Item(menu, "Always on top").Checked.Should().BeTrue();
    }

    [Fact]
    public void Reword_LanguageChangedSinceBuilt_WordsTheItemsInTheNewOne()
    {
        // Arrange
        using var menu = Menu();

        // Act
        Speak(Ukrainian);
        menu.Reword();

        // Assert
        menu.Entries.Select(item => item.Text).Should().Contain([Translated("Hide panel"), Translated("Exit")]);
    }

    [Fact]
    public void PerformClick_ItemsOfTheShell_ReachTheShell()
    {
        // Arrange
        using var menu = Menu();

        // Act
        Item(menu, "Hide panel").PerformClick();
        Item(menu, "Details…").PerformClick();
        Item(menu, "Settings…").PerformClick();
        Item(menu, "Exit").PerformClick();

        // Assert
        _shell.Calls.Should().Equal(
            nameof(IShell.ToggleOverlay),
            nameof(IShell.ShowDetails),
            nameof(IShell.ShowSettings),
            nameof(IShell.Exit));
    }

    [Fact]
    public void PerformClick_Switch_TurnsItOver()
    {
        // Arrange
        using var menu = Menu();

        // Act
        Item(menu, "Let clicks through").PerformClick();

        // Assert
        _station.Settings.Current.Overlay.IsClickThrough.Should().BeTrue();
    }

    private static IEnumerable<(string Text, bool IsChecked)> Items(TrayMenu menu) =>
        menu.Items.Cast<ToolStripItem>().Select(item => item switch
        {
            ToolStripMenuItem entry => (entry.Text ?? string.Empty, entry.Checked),
            _ => (Separator, false),
        });

    private static ToolStripMenuItem Item(TrayMenu menu, string text) => menu.Entries.Single(item => item.Text == text);

    private TrayMenu Menu() => new(_shell, new PanelMenu(_shell, _station.Settings, _station.Monitor));
}

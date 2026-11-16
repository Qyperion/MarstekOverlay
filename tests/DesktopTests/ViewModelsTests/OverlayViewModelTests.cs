using FluentAssertions;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewModelsTests;

public class OverlayViewModelTests : LocalizedTests
{
    private static readonly DateTimeOffset Start = Moments.Local(14, 12, 0);

    private readonly RecordingShell _shell = new();
    private readonly RecordingIndicator _indicator = new();

    [Fact]
    public void Open_NoStationSet_AsksForTheSettings() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start, isStationSet: false);

        // Act
        using var overlay = Open(station);

        // Assert
        overlay.State.StateText.Should().Be("Settings required");
        _indicator.States.Should().NotBeEmpty();
        _indicator.States[^1].Should().BeSameAs(overlay.State);
    });

    [Fact]
    public void Show_AnswerHandedOver_IsWordedForThePanelAndTheNotificationArea() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);

        // Act
        station.Answer(at => StationReadings.Charging(at, soc: 80));

        // Assert
        overlay.State.StateText.Should().Be("On mains · charging");
        overlay.State.SocText.Should().Be("80%");
        _indicator.States[^1].Should().BeSameAs(overlay.State);
    });

    [Fact]
    public void FollowVisibility_PanelShown_WordsTheStateByTheTimeItIsNow() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);
        station.Answer(at => StationReadings.Charging(at, soc: 80));
        station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 79));
        station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 79));
        var justConfirmed = overlay.State.StateText;
        station.Time.Advance(TimeSpan.FromSeconds(90));
        var beforeShown = overlay.State.StateText;

        // Act
        overlay.FollowVisibility(isVisible: true);
        overlay.FollowVisibility(isVisible: false);

        // Assert
        justConfirmed.Should().Be("On battery · less than a minute");
        beforeShown.Should().Be(justConfirmed, "between answers only the clock words the state anew");
        overlay.State.StateText.Should().Be("On battery · 2 min");
    });

    [Fact]
    public void Show_OutageNoticedOnAFullBattery_SaysItMayHaveBegunEarlier() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at));

        // Act
        for (var poll = 0; poll < 5; poll++)
            station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 99));

        // Assert
        overlay.State.StateText.Should().Be("On battery · at least 2 min");
        overlay.State.StateHint.Should().Contain("The app did not see when that began, so it may have been longer.");
    });

    [Fact]
    public void ApplySettings_PanelMadeCompact_FollowsAndLeavesTheChartOut() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at));
        var chartBefore = overlay.State.Chart;

        // Act
        station.Settings.Change(current => current.Overlay.IsCompact = true);

        // Assert
        chartBefore.Should().NotBeNull();
        overlay.IsCompact.Should().BeTrue();
        overlay.State.Chart.Should().BeNull();
    });

    [Fact]
    public void ApplySettings_ChartSwitchedOff_LeavesItOut() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at));
        var chartBefore = overlay.State.Chart;

        // Act
        station.Settings.Change(current => current.Overlay.IsChartShown = false);

        // Assert
        chartBefore.Should().NotBeNull();
        overlay.IsCompact.Should().BeFalse();
        overlay.State.Chart.Should().BeNull();
    });

    [Fact]
    public void ChartColumns_ToldByTheChart_DrawItAPointPerPixelOfThePanelAsScaled() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        station.Settings.Change(current =>
        {
            current.Overlay.ChartHours = 24;
            current.Overlay.ScalePercent = 150;
        });
        using var overlay = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at));

        // Act
        overlay.ChartColumns = 330;

        // Assert
        overlay.State.Chart?.Chart.PointReach.Should().Be(TimeSpan.FromHours(24) / 495);
    });

    [Fact]
    public void Menu_AlwaysOnTopChosen_IsSavedAndShown() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);
        var wasTopmost = overlay.IsTopmost;

        // Act
        overlay.Menu.Groups().SelectMany(group => group).Single(entry => entry.Text == "Always on top").Choose();

        // Assert
        overlay.IsTopmost.Should().Be(!wasTopmost);
        Reopened(station).Current.Overlay.IsTopmost.Should().Be(!wasTopmost);
    });

    [Fact]
    public void RememberPosition_PanelMoved_IsWhereItOpensNextTime() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);

        // Act
        overlay.RememberPosition(120, 340);

        // Assert
        overlay.Position.Should().Be((120, 340));
        var saved = Reopened(station).Current.Overlay;
        (saved.Left, saved.Top).Should().Be((120, 340));
    });

    [Fact]
    public void Commands_OfTheHeading_ReachTheShell() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);

        // Act
        overlay.OpenSettingsCommand.Execute(null);
        overlay.OpenDetailsCommand.Execute(null);
        overlay.HideCommand.Execute(null);

        // Assert
        _shell.Calls.Should().Equal(nameof(IShell.ShowSettings), nameof(IShell.ShowDetails), nameof(IShell.HideOverlay));
    });

    [Fact]
    public void LanguageChanged_StateIsWordedAnew() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        using var overlay = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at));

        // Act
        Speak(Ukrainian);

        // Assert
        overlay.State.StateText.Should().Be(Translated("On mains"));
    });

    [Fact]
    public void Dispose_PanelClosed_NoLongerFollowsTheStationTheLanguageOrTheSettings() => UiThread.Run(() =>
    {
        // Arrange
        using var station = new StationHarness(Start);
        var overlay = Open(station);
        station.Answer(at => StationReadings.HoldingFull(at));
        var before = overlay.State;
        var indicated = _indicator.States.Count;

        // Act
        overlay.Dispose();
        station.AnswerAfterInterval(at => StationReadings.Charging(at, soc: 80));
        Speak(Ukrainian);
        station.Settings.Change(current => current.Overlay.IsCompact = true);

        // Assert
        overlay.State.Should().BeSameAs(before);
        overlay.IsCompact.Should().BeFalse();
        _indicator.States.Should().HaveCount(indicated);
    });

    private OverlayViewModel Open(StationHarness station) => new(
        _shell,
        new PanelMenu(_shell, station.Settings, station.Monitor),
        station.Settings,
        station.Monitor,
        station.Pipeline,
        station.History,
        _indicator,
        Localizer.Instance,
        Options.Create(station.Polling),
        station.Time);

    private static UserSettingsStore Reopened(StationHarness station) =>
        new(station.Storage, NullLogger<UserSettingsStore>.Instance);

    private sealed class RecordingIndicator : IStatusIndicator
    {
        public List<OverlayState> States { get; } = [];

        public void Indicate(OverlayState state) => States.Add(state);
    }
}

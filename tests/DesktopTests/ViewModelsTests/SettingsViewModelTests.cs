using FluentAssertions;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewModelsTests;

public sealed class SettingsViewModelTests : LocalizedTests
{
    private readonly StationHarness _station = new(Moments.Local(14, 12, 0));
    private readonly StorageSettings _storage;
    private readonly UserSettingsStore _store;
    private readonly DiagnosticLog _log;
    private readonly List<StationProbeViewModel> _probes = [];
    private int _changes;

    public SettingsViewModelTests()
    {
        _storage = _station.Storage;
        _store = _station.Settings;
        _store.Changed += () => _changes++;
        _log = new DiagnosticLog(_storage, Options.Create(new LoggerOptions()));
    }

    public override void Dispose()
    {
        foreach (var probe in _probes)
            probe.Dispose();

        _log.Dispose();
        _station.Dispose();
        base.Dispose();
    }

    [Fact]
    public void Start_SwitchMadeFromAMenu_ShowsInTheOpenWindowWithoutChangingTheSettingsAgain()
    {
        // Arrange
        using var window = Open();
        window.Start();

        // Act
        _store.Change(current => current.Overlay.IsCompact = true);
        _store.Change(current => current.Overlay.IsTopmost = true);
        _store.Change(current => current.Overlay.IsClickThrough = true);

        // Assert
        window.IsCompact.Should().BeTrue();
        window.IsTopmost.Should().BeTrue();
        window.IsClickThrough.Should().BeTrue();
        _changes.Should().Be(3);
    }

    [Fact]
    public void Start_SwitchMadeInTheWindow_IsTriedOutWithoutBeingWritten()
    {
        // Arrange
        using var window = Open();
        window.Start();

        // Act
        window.IsCompact = true;

        // Assert
        _store.Current.Overlay.IsCompact.Should().BeTrue();
        _changes.Should().Be(1);
        File.Exists(_storage.UserSettingsPath).Should().BeFalse();
    }

    [Fact]
    public void IsHiddenAtStartup_Ticked_IsKeptForTheNextStartWithoutBeingWritten()
    {
        // Arrange
        using var window = Open();

        // Act
        window.IsHiddenAtStartup = true;

        // Assert
        _store.Current.Overlay.IsHiddenAtStartup.Should().BeTrue();
        _changes.Should().Be(1);
        File.Exists(_storage.UserSettingsPath).Should().BeFalse();
    }

    [Fact]
    public void IsHiddenAtStartup_AsTheWindowOpens_IsWhatWasChosenBefore()
    {
        // Arrange
        _store.Current.Overlay.IsHiddenAtStartup = true;

        // Act
        using var window = Open();

        // Assert
        window.IsHiddenAtStartup.Should().BeTrue();
        _changes.Should().Be(0);
    }

    [Fact]
    public void Dispose_WindowClosed_NoLongerFollowsTheMenus()
    {
        // Arrange
        var window = Open();
        window.Start();

        // Act
        window.Dispose();
        _store.Change(current => current.Overlay.IsCompact = true);

        // Assert
        window.IsCompact.Should().BeFalse();
    }

    [Fact]
    public void Dispose_SecondTime_DoesNothing()
    {
        // Arrange
        var window = Open();
        window.Start();
        window.Dispose();

        // Act
        var disposeAgain = window.Dispose;

        // Assert
        disposeAgain.Should().NotThrow();
    }

    [Fact]
    public void OpacityText_SliderMoved_FollowsItWithTheUnit()
    {
        // Arrange
        using var window = Open();
        var announced = new List<string?>();
        window.PropertyChanged += (_, change) => announced.Add(change.PropertyName);

        // Act
        window.OpacityPercent = 65;

        // Assert
        window.OpacityText.Should().Be("65%");
        announced.Should().Contain(nameof(SettingsViewModel.OpacityText));
    }

    [Fact]
    public void ChartPeriods_HandEditedPeriodThatIsNotOffered_MarksTheNearestOneBelow()
    {
        // Arrange
        _store.Current.Overlay.ChartHours = 5;

        // Act
        using var window = Open();

        // Assert
        window.ChartPeriods.Where(period => period.IsSelected).Should().ContainSingle()
            .Which.Name.Should().Be(Phrases.Period(TimeSpan.FromHours(3)));
    }

    [Fact]
    public void ChargeThresholds_AsTheWindowOpens_AreHighestFirstAndApartBySemicolons()
    {
        // Arrange
        _store.Current.Alerts.ChargeThresholdsPercent = [15, 50, 12.5];

        // Act
        using var window = Open();

        // Assert
        window.ChargeThresholds.Should().Be("50; 15; 12.5");
    }

    [Fact]
    public void Save_BothEfficiencies_AreKeptEachForItsOwnDirection()
    {
        // Arrange
        using var window = Open();
        var isClosed = false;
        window.CloseRequested += () => isClosed = true;
        window.DischargeEfficiencyPercent = "88";
        window.ChargeEfficiencyPercent = "96,5";

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        _store.Current.Forecast.DischargeEfficiencyPercent.Should().Be(88);
        _store.Current.Forecast.ChargeEfficiencyPercent.Should().Be(96.5);
        isClosed.Should().BeTrue();
    }

    [Theory]
    [InlineData(English, "40", "97", nameof(SettingsViewModel.DischargeEfficiencyPercent))]
    [InlineData(Ukrainian, "90", "101", nameof(SettingsViewModel.ChargeEfficiencyPercent))]
    [InlineData(English, "90", "", nameof(SettingsViewModel.ChargeEfficiencyPercent))]
    [InlineData(English, "NaN", "97", nameof(SettingsViewModel.DischargeEfficiencyPercent))]
    [InlineData(English, "90", "Infinity", nameof(SettingsViewModel.ChargeEfficiencyPercent))]
    public void Save_EfficiencyOutOfRange_IsTurnedDownWithTheLimitsNamedAtItsField(
        string language,
        string discharge,
        string charge,
        string marked)
    {
        // Arrange
        Speak(language);
        using var window = Open();
        window.DischargeEfficiencyPercent = discharge;
        window.ChargeEfficiencyPercent = charge;

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.Status.Message.IsError.Should().BeTrue();
        window.Status.Message.Text.Should().Be(Translated("The values marked in red need correcting."));
        window.Errors.Fields.Should().Equal(marked);
        window.Errors[marked].Should().Be(Translated("The efficiency must be from 50 to 100%."));
        _store.Current.Forecast.DischargeEfficiencyPercent.Should().Be(90);
        _store.Current.Forecast.ChargeEfficiencyPercent.Should().Be(97);
    }

    [Theory]
    [InlineData("50, 30, 12,5", new[] { 50, 30, 12.5 })]
    [InlineData("50; 30; 12.5", new[] { 50, 30, 12.5 })]
    [InlineData("15 50 15", new[] { 50.0, 15 })]
    [InlineData("50,5", new[] { 50.5 })]
    [InlineData("", new double[0])]
    public void Save_ChargeThresholds_AreSplitAtSpacesAndSemicolonsAndKeptHighestFirst(string typed, double[] saved)
    {
        // Arrange
        using var window = Open();
        window.ChargeThresholds = typed;

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.HasErrors.Should().BeFalse();
        _store.Current.Alerts.ChargeThresholdsPercent.Should().Equal(saved);
    }

    [Theory]
    [InlineData(English, "50 nan 15")]
    [InlineData(Ukrainian, "50 0 15")]
    [InlineData(English, "50,30,15")]
    [InlineData(English, "∞")]
    public void Save_ChargeThresholdThatIsNoPercentage_IsTurnedDownAndNothingIsSaved(string language, string typed)
    {
        // Arrange
        Speak(language);
        using var window = Open();
        window.ChargeThresholds = typed;

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.Errors[nameof(SettingsViewModel.ChargeThresholds)].Should().Be(
            Translated("The charge thresholds must be numbers from 1 to 100, separated by spaces or semicolons."));
        _store.Current.Alerts.ChargeThresholdsPercent.Should().Equal(50, 30, 15);
    }

    [Fact]
    public void Save_SeveralWrongValues_MarksThemAllAndOpensTheTabOfTheFirst()
    {
        // Arrange
        using var window = Open();
        var isClosed = false;
        window.CloseRequested += () => isClosed = true;
        window.SelectedTabIndex = (int)SettingsTab.Panel;
        window.Probe.Port = "0";
        window.StaleAfterMinutes = "soon";
        window.ReserveSocPercent = "150";

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.Probe.Errors.Fields.Should().Equal(nameof(StationProbeViewModel.Port));
        window.Errors.Fields.Should().BeEquivalentTo(
            nameof(SettingsViewModel.StaleAfterMinutes),
            nameof(SettingsViewModel.ReserveSocPercent));
        window.Probe.Errors[nameof(StationProbeViewModel.Port)].Should().Be(Translated("The port must be a number from 1 to 65535."));
        window.Errors[nameof(SettingsViewModel.ReserveSocPercent)].Should().Be(Translated("The reserve level must be from 0 to 99%."));
        window.SelectedTabIndex.Should().Be((int)SettingsTab.Station);
        _store.Current.Station.Port.Should().Be(30000);
        isClosed.Should().BeFalse();
    }

    [Fact]
    public void Save_WrongValueOnTheOpenTab_LeavesThatTabOpen()
    {
        // Arrange
        using var window = Open();
        window.SelectedTabIndex = (int)SettingsTab.Alerts;
        window.Probe.Port = "0";
        window.StaleAfterMinutes = "soon";

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.SelectedTabIndex.Should().Be((int)SettingsTab.Alerts);
    }

    [Fact]
    public void Save_TemperatureLimitsTheWrongWayRound_MarksBothWithOneMessage()
    {
        // Arrange
        using var window = Open();
        window.MinBatteryTemperature = "40";
        window.MaxBatteryTemperature = "30";

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.Errors.Fields.Should().BeEquivalentTo(
            nameof(SettingsViewModel.MinBatteryTemperature),
            nameof(SettingsViewModel.MaxBatteryTemperature));
        window.Errors[nameof(SettingsViewModel.MinBatteryTemperature)]
            .Should().Be(window.Errors[nameof(SettingsViewModel.MaxBatteryTemperature)]);
    }

    [Fact]
    public void Save_UnfinishedAddress_IsTurnedDownRatherThanSavedAsAnotherOne()
    {
        // Arrange
        using var window = Open();
        window.Probe.Host = "192.168.1";

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.Probe.Errors[nameof(StationProbeViewModel.Host)]
            .Should().Be(Translated("The station address must be an IPv4 address such as 192.168.1.50."));
        _store.Current.Station.Host.Should().Be("192.168.1.50");
    }

    [Fact]
    public void Edit_FieldThatWasMarked_IsNoLongerMarkedAndSaysSoToTheWindow()
    {
        // Arrange
        using var window = Open();
        var announced = new List<string?>();
        window.StaleAfterMinutes = "0";
        window.SaveCommand.Execute(null);
        window.ErrorsChanged += (_, change) => announced.Add(change.PropertyName);

        // Act
        window.StaleAfterMinutes = "10";

        // Assert
        window.HasErrors.Should().BeFalse();
        window.GetErrors(nameof(SettingsViewModel.StaleAfterMinutes)).Cast<string>().Should().BeEmpty();
        announced.Should().Equal(nameof(SettingsViewModel.StaleAfterMinutes));
    }

    [Fact]
    public void Edit_OneOfTheTemperatureLimits_UnmarksBoth()
    {
        // Arrange
        using var window = Open();
        window.MinBatteryTemperature = "40";
        window.MaxBatteryTemperature = "30";
        window.SaveCommand.Execute(null);

        // Act
        window.MaxBatteryTemperature = "45";

        // Assert
        window.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void GetErrors_MarkedField_IsItsMessage()
    {
        // Arrange
        using var window = Open();
        window.ReserveSocPercent = "100";

        // Act
        window.SaveCommand.Execute(null);

        // Assert
        window.HasErrors.Should().BeTrue();
        window.GetErrors(nameof(SettingsViewModel.ReserveSocPercent)).Cast<string>()
            .Should().Equal(Translated("The reserve level must be from 0 to 99%."));
        window.GetErrors(nameof(SettingsViewModel.StaleAfterMinutes)).Cast<string>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void StartsWithWindows_EntryThatCannotBeWritten_IsUntickedAgainWithTheReasonInTheLanguageOfTheApp(string language)
    {
        // Arrange
        Speak(language);
        using var window = Open(new LockedAutostart());

        // Act
        window.StartsWithWindows = true;

        // Assert
        window.StartsWithWindows.Should().BeFalse();
        window.Status.Message.Should().Be(new StatusMessage(
            Translated("The startup entry in the registry could not be changed: access is denied."),
            IsError: true));
    }

    [Fact]
    public void StartsWithWindows_AsTheWindowOpens_IsWhatWindowsIsSetTo()
    {
        // Act
        using var window = Open(new StoredAutostart { IsEnabled = true });

        // Assert
        window.StartsWithWindows.Should().BeTrue();
    }

    [Fact]
    public void StartsWithWindows_Ticked_WritesTheEntryAtOnceAndSaysNothing()
    {
        // Arrange
        var autostart = new StoredAutostart();
        using var window = Open(autostart);
        var before = window.Status.Message;

        // Act
        window.StartsWithWindows = true;

        // Assert
        autostart.IsEnabled.Should().BeTrue();
        window.StartsWithWindows.Should().BeTrue();
        window.Status.Message.Should().Be(before);
    }

    [Fact]
    public void Rows_RowTicked_IsTriedOutOnThePanelWithoutBeingWritten()
    {
        // Arrange
        using var window = Open();

        // Act
        window.Rows.Single(row => row.Name == OverlayRowKind.WifiSignal.DisplayName).IsChecked = true;
        window.Rows.Single(row => row.Name == OverlayRowKind.Solar.DisplayName).IsChecked = false;

        // Assert
        _store.Current.Overlay.Shows(OverlayRowKind.WifiSignal).Should().BeTrue();
        _store.Current.Overlay.Shows(OverlayRowKind.Solar).Should().BeFalse();
        _changes.Should().Be(2);
        File.Exists(_storage.UserSettingsPath).Should().BeFalse();
    }

    [Fact]
    public void ChartPeriods_PeriodPicked_IsTriedOutOnThePanelWithoutBeingWritten()
    {
        // Arrange
        using var window = Open();

        // Act
        window.ChartPeriods.Single(period => period.Name == Phrases.PeriodOfHours(12)).IsSelected = true;

        // Assert
        _store.Current.Overlay.ChartHours.Should().Be(12);
        File.Exists(_storage.UserSettingsPath).Should().BeFalse();
    }

    [Fact]
    public void Alerts_AlertUnticked_IsNoLongerWatched()
    {
        // Arrange
        using var window = Open();

        // Act
        window.Alerts.Single(alert => alert.Name == AlertKind.StaleData.DisplayName).IsChecked = false;

        // Assert
        _store.Current.Alerts.Watches(AlertKind.StaleData).Should().BeFalse();
        _store.Current.Alerts.Watches(AlertKind.MainsLost).Should().BeTrue();
        File.Exists(_storage.UserSettingsPath).Should().BeFalse();
    }

    [Fact]
    public void Languages_AnotherPicked_KeepsItForTheAppAndDropsTheMessagesWordedInTheOldOne()
    {
        // Arrange
        using var window = Open();
        window.ReserveSocPercent = "100";
        window.SaveCommand.Execute(null);

        // Act
        window.Languages.Single(language => language.Name == AppLanguages.NativeName(AppLanguages.Ukrainian)).IsSelected = true;

        // Assert
        _store.Current.Language.Should().Be(Ukrainian);
        window.HasErrors.Should().BeFalse();
        window.Status.Message.Should().Be(new StatusMessage(
            Translated("The app only reads from the station and never changes how it works."),
            IsError: false));
        File.Exists(_storage.UserSettingsPath).Should().BeFalse();
    }

    [Fact]
    public void MayClose_NothingTyped_ClosesAtOnce()
    {
        // Arrange
        using var window = Open();
        window.IsCompact = true;

        // Act
        var mayClose = window.MayClose();

        // Assert
        mayClose.Should().BeTrue("a switch is kept as it stands, with nothing to lose");
    }

    [Fact]
    public void MayClose_NumberTypedAndNotSaved_WarnsOnceAndThenLetsItGo()
    {
        // Arrange
        using var window = Open();
        window.ReserveSocPercent = "20";

        // Act
        var first = window.MayClose();
        var second = window.MayClose();

        // Assert
        first.Should().BeFalse();
        second.Should().BeTrue();
        window.Status.Message.IsError.Should().BeTrue();
        window.Status.Message.Text.Should().Be(Translated(
            "The address or the numbers were changed and not saved. Press Close once more to drop the changes, or Save to keep them."));
    }

    [Fact]
    public void MayClose_AddressTypedAndNotSaved_Warns()
    {
        // Arrange
        using var window = Open();
        window.Start();
        window.Probe.Host = "192.168.1.51";

        // Act
        var mayClose = window.MayClose();

        // Assert
        mayClose.Should().BeFalse();
    }

    [Fact]
    public void MayClose_EditAfterTheWarning_WarnsAgain()
    {
        // Arrange
        using var window = Open();
        window.ReserveSocPercent = "20";
        window.MayClose();

        // Act
        window.ReserveSocPercent = "25";
        var mayClose = window.MayClose();

        // Assert
        mayClose.Should().BeFalse();
    }

    [Fact]
    public void MayClose_WarningReplacedByAnotherMessage_WarnsAgain()
    {
        // Arrange
        using var window = Open();
        window.ReserveSocPercent = "20";
        window.MayClose();

        // Act
        window.Languages.Single(language => !language.IsSelected).IsSelected = true;
        var mayClose = window.MayClose();

        // Assert
        mayClose.Should().BeFalse();
        window.Status.Message.Text.Should().Be(Translated(
            "The address or the numbers were changed and not saved. Press Close once more to drop the changes, or Save to keep them."));
    }

    [Fact]
    public void MayClose_AfterSaving_ClosesAtOnce()
    {
        // Arrange
        using var window = Open();
        window.ReserveSocPercent = "20";

        // Act
        window.SaveCommand.Execute(null);
        var mayClose = window.MayClose();

        // Assert
        mayClose.Should().BeTrue();
        _store.Current.Forecast.ReserveSocPercent.Should().Be(20);
    }

    [Fact]
    public void Version_IsTheVersionOfTheAppInThreeNumbers()
    {
        // Act
        using var window = Open();

        // Assert
        window.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void StaleAfterInEffect_PollLongerThanHalfTheTimeTyped_SaysWhatTheAlertWaitsFor(string language)
    {
        // Arrange
        Speak(language);
        _store.Current.Alerts.StaleAfterMinutes = 1;

        // Act
        using var window = Open(polling: new PollingSettings { IntervalSeconds = 120, SlowIntervalSeconds = 300 });

        // Assert
        window.StaleAfterInEffect.Should().Be(Translated("In effect: 4 min, at least two poll intervals."));
    }

    [Fact]
    public void StaleAfterInEffect_TimeTyped_FollowsIt()
    {
        // Arrange
        using var window = Open(polling: new PollingSettings { IntervalSeconds = 120, SlowIntervalSeconds = 300 });
        var announced = new List<string?>();
        window.PropertyChanged += (_, change) => announced.Add(change.PropertyName);

        // Act
        window.StaleAfterMinutes = "3";

        // Assert
        window.StaleAfterInEffect.Should().Be("In effect: 4 min, at least two poll intervals.");
        announced.Should().Contain(nameof(SettingsViewModel.StaleAfterInEffect));
    }

    [Fact]
    public void StaleAfterInEffect_UsualPoll_IsNothing()
    {
        // Act
        using var window = Open();

        // Assert
        window.StaleAfterInEffect.Should().BeNull();
    }

    private SettingsViewModel Open(IAutostart? autostart = null, PollingSettings? polling = null)
    {
        var client = new UnreachableApiClient();
        var reader = new StationReader(
            client,
            Options.Create(new PollingSettings()),
            TimeProvider.System,
            NullLogger<StationReader>.Instance);
        var status = new StatusLine();
        var probe = new StationProbeViewModel(_store, client, reader, _log, status);
        _probes.Add(probe);

        return new SettingsViewModel(
            _store,
            new SilentChannel(),
            autostart ?? new StoredAutostart(),
            Localizer.Instance,
            status,
            probe,
            new StoredDataViewModel(_station.Pipeline, _log, _station.Time, NullLogger<StoredDataViewModel>.Instance),
            Options.Create(polling ?? new PollingSettings()));
    }

    private sealed class SilentChannel : INotificationChannel
    {
        public void Notify(Notification notification)
        {
        }
    }

    /// <summary>An entry kept in memory rather than in the registry of the machine the tests run on.</summary>
    private sealed class StoredAutostart : IAutostart
    {
        public bool IsEnabled { get; set; }

        public bool TrySet(bool isEnabled, [NotNullWhen(false)] out Exception? failure)
        {
            IsEnabled = isEnabled;
            failure = null;
            return true;
        }
    }

    private sealed class LockedAutostart : IAutostart
    {
        public bool IsEnabled => false;

        public bool TrySet(bool isEnabled, [NotNullWhen(false)] out Exception? failure)
        {
            failure = new UnauthorizedAccessException("Access is denied.");
            return false;
        }
    }
}

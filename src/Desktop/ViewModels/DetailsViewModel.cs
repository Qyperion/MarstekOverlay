using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>Everything the station reports, the outage journal and the charts, kept current for as long as the
/// details window is open. It only reads what the pipeline keeps up to date and must be disposed of with the
/// window, as it listens to objects that outlive it.</summary>
public sealed partial class DetailsViewModel : ObservableObject, IDisposable
{
    // Grouped by what the cards are about: the power, the device and how it is reached, and what a station may
    // come without.
    private static readonly string[][] CardLayout =
    [
        [MarstekMethods.EnergySystemGetStatus, MarstekMethods.BatteryGetStatus, MarstekMethods.EnergySystemGetMode],
        [MarstekMethods.GetDevice, MarstekMethods.WifiGetStatus, MarstekMethods.BluetoothGetStatus],
        [MarstekMethods.EnergyMeterGetStatus, MarstekMethods.PvGetStatus],
    ];

    private readonly UserSettingsStore _settings;
    private readonly StationMonitor _monitor;
    private readonly StationPipeline _pipeline;
    private readonly Localizer _localizer;
    private readonly PollingSettings _polling;
    private readonly TimeProvider _timeProvider;
    private readonly SecondsClock _clock;
    private readonly Dictionary<string, StationCardViewModel> _cards;

    public DetailsViewModel(
        UserSettingsStore settings,
        StationMonitor monitor,
        StationPipeline pipeline,
        OutageJournal outages,
        SampleHistory history,
        Localizer localizer,
        IOptions<HistorySettings> historyOptions,
        IOptions<PollingSettings> pollingOptions,
        TimeProvider timeProvider,
        ILogger<DetailsViewModel> logger)
    {
        _settings = settings;
        _monitor = monitor;
        _pipeline = pipeline;
        _localizer = localizer;
        _polling = pollingOptions.Value;
        _timeProvider = timeProvider;

        _cards = CardLayout.SelectMany(group => group).ToDictionary(method => method, _ => new StationCardViewModel());
        CardGroups = [.. CardLayout.Select(group => group.Select(method => _cards[method]).ToArray())];
        Journal = new JournalViewModel(outages, history, timeProvider, logger);
        Charts = new HistoryChartsViewModel(settings, history, historyOptions.Value, timeProvider);

        Refresh();

        pipeline.StateChanged += Refresh;
        pipeline.RecordsDeleted += ReadAnew;
        localizer.LanguageChanged += OnLanguageChanged;

        // The ages of the answers are told to the second.
        _clock = new SecondsClock(Refresh);
        _clock.Start();
    }

    public StationCardViewModel[][] CardGroups { get; }

    public JournalViewModel Journal { get; }

    public HistoryChartsViewModel Charts { get; }

    /// <summary>What went wrong in the latest poll; nothing after a clean one.</summary>
    [ObservableProperty]
    public partial string? Errors { get; private set; }

    /// <summary>Where the window was left the last time; nothing before it has been closed once.</summary>
    public SavedWindowPlace? WindowPlace => _settings.Current.Details.Window;

    public void RememberWindowPlace(SavedWindowPlace place) => _settings.Change(current => current.Details.Window = place);

    public void Dispose()
    {
        _clock.Stop();
        Charts.Stop();
        _pipeline.StateChanged -= Refresh;
        _pipeline.RecordsDeleted -= ReadAnew;
        _localizer.LanguageChanged -= OnLanguageChanged;
    }

    // The station is not asked again about a CT or solar inputs it has said it lacks; this is how it learns of
    // one connected since.
    [RelayCommand]
    private void CheckAccessories() => _monitor.CheckAbandoned();

    private void OnLanguageChanged()
    {
        Journal.RefreshTexts();
        Charts.RefreshTexts();
        Refresh();
    }

    // The journal is read again by the change of state that follows.
    private void ReadAnew()
    {
        Journal.ReadAnew();
        Charts.ReadAnew();
    }

    private void Refresh()
    {
        var now = _timeProvider.GetUtcNow();
        var state = _pipeline.Latest;
        var snapshot = state?.Snapshot ?? StationSnapshot.Empty;

        foreach (var section in StationSections.Describe(snapshot, state?.Supply.Source ?? PowerSource.Unknown))
            _cards[section.Method].Show(section, now, ReadingFreshness.TrustedFor(section.Method, _polling));

        Errors = ErrorPresenter.Describe(snapshot.Errors);

        Journal.Follow(now);
        Charts.Follow(now);
    }
}

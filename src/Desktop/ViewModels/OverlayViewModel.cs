using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Options;

namespace MarstekOverlay.Desktop.ViewModels;

public sealed partial class OverlayViewModel : ObservableObject, IDisposable
{
    // Until the chart has been laid out and tells its own width: about that of the panel at its own size.
    private const int ColumnsBeforeLayout = 220;

    private readonly IShell _shell;
    private readonly UserSettingsStore _settings;
    private readonly StationMonitor _monitor;
    private readonly StationPipeline _pipeline;
    private readonly SampleHistory _history;
    private readonly IStatusIndicator _indicator;
    private readonly Localizer _localizer;
    private readonly PollingSettings _polling;
    private readonly TimeProvider _timeProvider;
    private readonly SecondsClock _clock;

    // Drawn once per state, so that the clock can word the state again without building the chart every second.
    private HistoryChart? _chart;

    public OverlayViewModel(
        IShell shell,
        PanelMenu menu,
        UserSettingsStore settings,
        StationMonitor monitor,
        StationPipeline pipeline,
        SampleHistory history,
        IStatusIndicator indicator,
        Localizer localizer,
        IOptions<PollingSettings> polling,
        TimeProvider timeProvider)
    {
        _shell = shell;
        Menu = menu;
        _settings = settings;
        _monitor = monitor;
        _pipeline = pipeline;
        _history = history;
        _indicator = indicator;
        _localizer = localizer;
        _polling = polling.Value;
        _timeProvider = timeProvider;
        State = new OverlayState();

        pipeline.StateChanged += Show;

        // The state holds finished texts, so a new language needs it built again.
        localizer.LanguageChanged += Show;
        settings.Changed += ApplySettings;
        ApplySettings();

        // Readings turn stale and a duration moves on to its next minute at any second. Started once the panel
        // is shown.
        _clock = new SecondsClock(Present);
    }

    public PanelMenu Menu { get; }

    [ObservableProperty]
    public partial OverlayState State { get; private set; }

    [ObservableProperty]
    public partial bool IsTopmost { get; private set; }

    [ObservableProperty]
    public partial bool IsCompact { get; private set; }

    [ObservableProperty]
    public partial bool IsClickThrough { get; private set; }

    [ObservableProperty]
    public partial double CardOpacity { get; private set; }

    [ObservableProperty]
    public partial double Scale { get; private set; }

    /// <summary>Set by the chart: how many pixels across it draws its lines at the panel's own size.</summary>
    [ObservableProperty]
    public partial int ChartColumns { get; set; }

    public (double Left, double Top) Position => (_settings.Current.Overlay.Left, _settings.Current.Overlay.Top);

    /// <summary>A panel nobody sees has no use for the clock: every answer still words the state anew, which is
    /// as often as the notification-area icon needs it.</summary>
    public void FollowVisibility(bool isVisible)
    {
        if (!isVisible)
        {
            _clock.Stop();
            return;
        }

        Present();
        _clock.Start();
    }

    public void Dispose()
    {
        _clock.Stop();
        _pipeline.StateChanged -= Show;
        _localizer.LanguageChanged -= Show;
        _settings.Changed -= ApplySettings;
    }

    public void RememberPosition(double left, double top) =>
        _settings.Change(current =>
        {
            current.Overlay.Left = left;
            current.Overlay.Top = top;
        });

    [RelayCommand]
    private void Refresh() => _monitor.RequestRefresh();

    [RelayCommand]
    private void OpenSettings() => _shell.ShowSettings();

    [RelayCommand]
    private void OpenDetails() => _shell.ShowDetails();

    [RelayCommand]
    private void Hide() => _shell.HideOverlay();

    partial void OnChartColumnsChanged(int value) => Show();

    private void ApplySettings()
    {
        var overlay = _settings.Current.Overlay;
        (IsTopmost, IsCompact, IsClickThrough) = overlay.Modes();
        CardOpacity = overlay.Opacity();
        Scale = overlay.Scale();

        Show();
    }

    private void Show()
    {
        if (_pipeline.Latest is null)
        {
            _chart = null;
            Display(OverlayPresenter.SettingsRequired());
            return;
        }

        _chart = DrawChart(_timeProvider.GetUtcNow());
        Present();
    }

    private void Present()
    {
        if (_pipeline.Latest is not { } state)
            return;

        Display(OverlayPresenter.Present(
            state,
            _settings.Current.Forecast,
            _settings.Current.Alerts,
            _settings.Current.Overlay,
            _polling,
            _chart,
            _timeProvider.GetUtcNow()));
    }

    private void Display(OverlayState state)
    {
        State = state.KeepingListsOf(State);
        _indicator.Indicate(State);
    }

    private HistoryChart? DrawChart(DateTimeOffset now)
    {
        var overlay = _settings.Current.Overlay;
        if (!overlay.IsChartShown || overlay.IsCompact)
            return null;

        var from = now - overlay.ChartPeriod();
        var columns = (int)Math.Round((ChartColumns > 0 ? ChartColumns : ColumnsBeforeLayout) * overlay.Scale());

        return _history.Chart(from, now, columns);
    }
}

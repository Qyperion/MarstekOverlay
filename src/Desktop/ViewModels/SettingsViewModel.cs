using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Options;

using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace MarstekOverlay.Desktop.ViewModels;

public sealed partial class SettingsViewModel(
    UserSettingsStore settings,
    INotificationChannel notifications,
    IAutostart autostart,
    Localizer localizer,
    StatusLine status,
    StationProbeViewModel probe,
    StoredDataViewModel storedData,
    IOptions<PollingSettings> polling) : ObservableObject, INotifyDataErrorInfo, IDisposable
{
    private static readonly Dictionary<string, SettingsTab> TabsOfFields = new()
    {
        [nameof(StationProbeViewModel.Host)] = SettingsTab.Station,
        [nameof(StationProbeViewModel.Port)] = SettingsTab.Station,
        [nameof(ChargeThresholds)] = SettingsTab.Alerts,
        [nameof(MinBatteryTemperature)] = SettingsTab.Alerts,
        [nameof(MaxBatteryTemperature)] = SettingsTab.Alerts,
        [nameof(StaleAfterMinutes)] = SettingsTab.Alerts,
        [nameof(ReserveSocPercent)] = SettingsTab.General,
        [nameof(DischargeEfficiencyPercent)] = SettingsTab.General,
        [nameof(ChargeEfficiencyPercent)] = SettingsTab.General,
    };

    private bool _hasUnsavedEdits;
    private bool _isDiscardAnnounced;
    private bool _isDisposed;

    private delegate bool FieldReader<T>(string text, out T value, out string? error);

    public event Action? CloseRequested;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>The fields of this window's own that are saved only by the button, and so can be wrong when it is
    /// pressed; the address and the port are marked by <see cref="Probe"/>.</summary>
    public FieldErrors Errors => field ??= new FieldErrors(name => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(name)));

    public bool HasErrors => Errors.Any;

    public StatusLine Status { get; } = status;

    public StationProbeViewModel Probe { get; } = probe;

    public StoredDataViewModel StoredData { get; } = storedData;

    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? string.Empty;

    public IReadOnlyList<ChoiceOption<CultureInfo>> Languages => field ??= AppLanguages.All
        .Select(language => new ChoiceOption<CultureInfo>(
            language,
            AppLanguages.NativeName,
            language.Equals(localizer.Language),
            UseLanguage))
        .ToList();

    public IReadOnlyList<SwitchOption<OverlayRowKind>> Rows => field ??= Enum.GetValues<OverlayRowKind>()
        .Select(kind => new SwitchOption<OverlayRowKind>(kind, row => row.DisplayName, settings.Current.Overlay.Shows(kind), ShowRow))
        .ToList();

    public IReadOnlyList<ChoiceOption<int>> ChartPeriods => field ??= OverlaySettings.ChartHoursChoices
        .Select(hours => new ChoiceOption<int>(
            hours,
            Phrases.PeriodOfHours,
            hours == OverlaySettings.OfferedChartHours(settings.Current.Overlay.ChartHours),
            UseChartPeriod))
        .ToList();

    public IReadOnlyList<SwitchOption<AlertKind>> Alerts => field ??= Enum.GetValues<AlertKind>()
        .Select(kind => new SwitchOption<AlertKind>(kind, alert => alert.DisplayName, settings.Current.Alerts.Watches(kind), WatchAlert))
        .ToList();

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [ObservableProperty]
    public partial bool IsTopmost { get; set; } = settings.Current.Overlay.IsTopmost;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpacityText))]
    public partial int OpacityPercent { get; set; } = settings.Current.Overlay.OpacityPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleText))]
    public partial int ScalePercent { get; set; } = settings.Current.Overlay.ScalePercent;

    public string OpacityText => Units.Percent(OpacityPercent);

    public string ScaleText => Units.Percent(ScalePercent);

    [ObservableProperty]
    public partial bool IsCompact { get; set; } = settings.Current.Overlay.IsCompact;

    [ObservableProperty]
    public partial bool IsClickThrough { get; set; } = settings.Current.Overlay.IsClickThrough;

    [ObservableProperty]
    public partial bool IsHiddenAtStartup { get; set; } = settings.Current.Overlay.IsHiddenAtStartup;

    [ObservableProperty]
    public partial bool StartsWithWindows { get; set; } = autostart.IsEnabled;

    [ObservableProperty]
    public partial bool IsChartShown { get; set; } = settings.Current.Overlay.IsChartShown;

    [ObservableProperty]
    public partial string ReserveSocPercent { get; set; } = SettingsInput.Number(settings.Current.Forecast.ReserveSocPercent);

    [ObservableProperty]
    public partial string DischargeEfficiencyPercent { get; set; } =
        SettingsInput.Number(settings.Current.Forecast.DischargeEfficiencyPercent);

    [ObservableProperty]
    public partial string ChargeEfficiencyPercent { get; set; } =
        SettingsInput.Number(settings.Current.Forecast.ChargeEfficiencyPercent);

    [ObservableProperty]
    public partial string ChargeThresholds { get; set; } =
        SettingsInput.ChargeThresholds(settings.Current.Alerts.UsableChargeThresholds());

    [ObservableProperty]
    public partial string MinBatteryTemperature { get; set; } =
        SettingsInput.Number(settings.Current.Alerts.MinBatteryTemperatureCelsius);

    [ObservableProperty]
    public partial string MaxBatteryTemperature { get; set; } =
        SettingsInput.Number(settings.Current.Alerts.MaxBatteryTemperatureCelsius);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StaleAfterInEffect))]
    public partial string StaleAfterMinutes { get; set; } = SettingsInput.Number(settings.Current.Alerts.StaleAfterMinutes);

    /// <summary>The station is heard from once a poll, so a silence shorter than two of them goes unnoticed.</summary>
    public string? StaleAfterInEffect => SettingsInput.StaleAfterInEffect(StaleAfterMinutes, polling.Value);

    /// <summary>The menus of the panel and of the notification-area icon switch some of what this window shows,
    /// and they stay within reach while it is open.</summary>
    public void Start()
    {
        settings.Changed += FollowMenus;
        Probe.Edited += MarkUnsaved;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        settings.Changed -= FollowMenus;
        Probe.Edited -= MarkUnsaved;
    }

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && Errors[propertyName] is { } message ? [message] : Array.Empty<string>();

    /// <summary>Closing the window drops the address and the numbers that were typed and not saved, unlike the
    /// switches, which are kept as they stand. The first attempt after such an edit only says so, and says it
    /// again if something else has taken its place on the status line.</summary>
    public bool MayClose()
    {
        if (!_hasUnsavedEdits || (_isDiscardAnnounced && Status.Message == SettingsMessages.UnsavedChanges))
            return true;

        _isDiscardAnnounced = true;
        Status.Message = SettingsMessages.UnsavedChanges;
        return false;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is { } field && TabsOfFields.ContainsKey(field))
            Edited(field);
    }

    private void FollowMenus() => (IsTopmost, IsCompact, IsClickThrough) = settings.Current.Overlay.Modes();

    // Appearance is tried out live on the panel and written to disk once, when the window closes. What a menu
    // has switched is already there and is merely shown.
    partial void OnIsTopmostChanged(bool value) => TryOut(overlay => overlay.IsTopmost, value, (overlay, set) => overlay.IsTopmost = set);

    partial void OnOpacityPercentChanged(int value) =>
        TryOut(overlay => overlay.OpacityPercent, value, (overlay, set) => overlay.OpacityPercent = set);

    partial void OnScalePercentChanged(int value) =>
        TryOut(overlay => overlay.ScalePercent, value, (overlay, set) => overlay.ScalePercent = set);

    partial void OnIsCompactChanged(bool value) => TryOut(overlay => overlay.IsCompact, value, (overlay, set) => overlay.IsCompact = set);

    partial void OnIsClickThroughChanged(bool value) =>
        TryOut(overlay => overlay.IsClickThrough, value, (overlay, set) => overlay.IsClickThrough = set);

    partial void OnIsHiddenAtStartupChanged(bool value) =>
        TryOut(overlay => overlay.IsHiddenAtStartup, value, (overlay, set) => overlay.IsHiddenAtStartup = set);

    partial void OnIsChartShownChanged(bool value) =>
        TryOut(overlay => overlay.IsChartShown, value, (overlay, set) => overlay.IsChartShown = set);

    private void TryOut<T>(Func<OverlaySettings, T> current, T value, Action<OverlaySettings, T> set)
    {
        if (!EqualityComparer<T>.Default.Equals(current(settings.Current.Overlay), value))
            settings.Change(changed => set(changed.Overlay, value), persist: false);
    }

    // Unlike the rest, this lives in the registry and takes effect there at once. A write that failed leaves
    // the box showing what Windows is going to do.
    partial void OnStartsWithWindowsChanged(bool value)
    {
        if (value == autostart.IsEnabled || autostart.TrySet(value, out var failure))
            return;

        Status.Message = SettingsMessages.AutostartFailed(failure);
        StartsWithWindows = autostart.IsEnabled;
    }

    // A field being corrected is no longer marked: whether it is right now is found out by the next attempt.
    // The two temperature limits are right or wrong together.
    private void Edited(string field)
    {
        if (field is nameof(MinBatteryTemperature) or nameof(MaxBatteryTemperature))
        {
            Errors.Set(nameof(MinBatteryTemperature), null);
            Errors.Set(nameof(MaxBatteryTemperature), null);
        }
        else
        {
            Errors.Set(field, null);
        }

        MarkUnsaved();
    }

    private void MarkUnsaved()
    {
        _hasUnsavedEdits = true;
        _isDiscardAnnounced = false;
    }

    private void ShowRow(OverlayRowKind kind, bool isShown) =>
        settings.Change(current => current.Overlay.Show(kind, isShown), persist: false);

    private void UseChartPeriod(int hours) =>
        settings.Change(current => current.Overlay.ChartHours = hours, persist: false);

    private void WatchAlert(AlertKind kind, bool isWatched) =>
        settings.Change(current => current.Alerts.Watch(kind, isWatched), persist: false);

    private void UseLanguage(CultureInfo language)
    {
        settings.Change(current => current.Language = language.Name, persist: false);

        // The window's own labels follow the switch through their bindings; these texts were composed earlier.
        foreach (var row in Rows)
            row.RefreshName();

        foreach (var period in ChartPeriods)
            period.RefreshName();

        foreach (var alert in Alerts)
            alert.RefreshName();

        OnPropertyChanged(nameof(OpacityText));
        OnPropertyChanged(nameof(ScaleText));
        OnPropertyChanged(nameof(StaleAfterInEffect));
        StoredData.RefreshTexts();
        Probe.RefreshTexts();

        // Their messages were worded in the language just left.
        Errors.Clear();

        Status.Message = SettingsMessages.ReadOnlyNote;
    }

    [RelayCommand]
    private void ShowTestNotification() =>
        notifications.Notify(new Notification(Strings.Common_ProductName, Strings.Alert_TestText, Tone.Info));

    [RelayCommand]
    private void Save()
    {
        // Every field is read, a wrong one or not, so that all of the wrong ones are marked at once.
        var isValid = Probe.TryReadEndpoint(out var station)
            & TryRead(nameof(ReserveSocPercent), ReserveSocPercent, SettingsInput.TryReadReserve, out double reserveSocPercent)
            & TryRead(
                nameof(DischargeEfficiencyPercent),
                DischargeEfficiencyPercent,
                SettingsInput.TryReadEfficiency,
                out double dischargeEfficiencyPercent)
            & TryRead(
                nameof(ChargeEfficiencyPercent),
                ChargeEfficiencyPercent,
                SettingsInput.TryReadEfficiency,
                out double chargeEfficiencyPercent)
            & TryRead(
                nameof(ChargeThresholds),
                ChargeThresholds,
                SettingsInput.TryReadChargeThresholds,
                out IReadOnlyList<double> chargeThresholds)
            & TryReadTemperatureLimits(out var minTemperature, out var maxTemperature)
            & TryRead(nameof(StaleAfterMinutes), StaleAfterMinutes, SettingsInput.TryReadStaleAfter, out double staleAfterMinutes);

        if (!isValid || station is null)
        {
            ShowMarkedFields();
            return;
        }

        settings.Change(current =>
        {
            current.Station.Host = station.Address.ToString();
            current.Station.Port = station.Port;
            current.Forecast.ReserveSocPercent = reserveSocPercent;
            current.Forecast.DischargeEfficiencyPercent = dischargeEfficiencyPercent;
            current.Forecast.ChargeEfficiencyPercent = chargeEfficiencyPercent;
            current.Alerts.ChargeThresholdsPercent = [.. chargeThresholds];
            current.Alerts.MinBatteryTemperatureCelsius = minTemperature;
            current.Alerts.MaxBatteryTemperatureCelsius = maxTemperature;
            current.Alerts.StaleAfterMinutes = staleAfterMinutes;
        });

        _hasUnsavedEdits = false;
        CloseRequested?.Invoke();
    }

    private bool TryRead<T>(string field, string text, FieldReader<T> read, out T value)
    {
        var isValid = read(text, out value, out var error);

        Errors.Set(field, error);
        return isValid;
    }

    private bool TryReadTemperatureLimits(out double min, out double max)
    {
        var isValid = SettingsInput.TryReadTemperatureLimits(MinBatteryTemperature, MaxBatteryTemperature, out min, out max, out var error);

        Errors.Set(nameof(MinBatteryTemperature), error);
        Errors.Set(nameof(MaxBatteryTemperature), error);
        return isValid;
    }

    /// <summary>Says that something is marked and brings a marked field into view, staying on the tab that is
    /// open if it has one.</summary>
    private void ShowMarkedFields()
    {
        Status.Message = SettingsMessages.FixMarked;

        var tabs = Errors.Fields.Concat(Probe.Errors.Fields).Select(marked => TabsOfFields[marked]).Distinct().Order().ToList();
        if (tabs.Count > 0 && !tabs.Contains((SettingsTab)SelectedTabIndex))
            SelectedTabIndex = (int)tabs[0];
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Settings;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>The tab of the details window with the charts of the samples over a period the user picks. Kept
/// current by <see cref="DetailsViewModel"/>.</summary>
public sealed partial class HistoryChartsViewModel : ObservableObject
{
    private const int HoursPerDay = 24;

    // Until the chart has been laid out and tells its own width.
    private const int ColumnsBeforeLayout = 800;

    private readonly UserSettingsStore _settings;
    private readonly TimeProvider _timeProvider;
    private readonly TrailingSamples _samples;

    private DateTimeOffset _drawnAt;
    private bool _isStopped;

    public HistoryChartsViewModel(UserSettingsStore settings, SampleHistory history, HistorySettings kept, TimeProvider timeProvider)
    {
        _settings = settings;
        _timeProvider = timeProvider;

        var keptHours = kept.RetentionDays * HoursPerDay;
        var offered = DetailsSettings.ChartHoursChoices.Where(hours => hours <= keptHours).ToList();
        var chosen = offered.LastOrDefault(hours => hours <= settings.Current.Details.ChartHours, offered[0]);
        Periods = [.. offered.Select(hours => new ChoiceOption<int>(hours, Phrases.PeriodOfHours, hours == chosen, UsePeriod))];

        var now = timeProvider.GetUtcNow();
        _samples = new TrailingSamples(history, TimeSpan.FromHours(chosen), now);
        Draw(now);
    }

    public IReadOnlyList<ChoiceOption<int>> Periods { get; }

    [ObservableProperty]
    public partial HistoryChart? Chart { get; private set; }

    /// <summary>The summary of the charts or, while the pointer is over them, the values under it.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    /// <summary>Set by the chart: the moment under the pointer.</summary>
    [ObservableProperty]
    public partial DateTimeOffset? Pointer { get; set; }

    /// <summary>Set by the chart: how many pixels across it draws its lines.</summary>
    [ObservableProperty]
    public partial int Columns { get; set; }

    private int ColumnsToDraw => Columns > 0 ? Columns : ColumnsBeforeLayout;

    /// <summary>Adds what was recorded since and moves the charts on with the clock, a column at a time.</summary>
    public void Follow(DateTimeOffset now)
    {
        var hasNews = _samples.Follow();

        if (hasNews || Chart is not { } chart || now - _drawnAt >= (chart.To - chart.From) / ColumnsToDraw)
            Draw(now);
    }

    /// <summary>What was read may be gone.</summary>
    public void ReadAnew()
    {
        var now = _timeProvider.GetUtcNow();

        _samples.Read(_samples.Period, now);
        Draw(now);
    }

    public void RefreshTexts()
    {
        foreach (var period in Periods)
            period.RefreshName();

        Describe();
    }

    /// <summary>The window is closed: a chart laid out once more on its way out draws nothing.</summary>
    public void Stop() => _isStopped = true;

    partial void OnPointerChanged(DateTimeOffset? value) => Describe();

    partial void OnColumnsChanged(int value) => Draw(_timeProvider.GetUtcNow());

    private void UsePeriod(int hours)
    {
        _settings.Change(current => current.Details.ChartHours = hours);

        var now = _timeProvider.GetUtcNow();
        _samples.Read(TimeSpan.FromHours(hours), now);
        Draw(now);
    }

    private void Draw(DateTimeOffset now)
    {
        if (_isStopped)
            return;

        _drawnAt = now;
        Chart = _samples.Chart(now, ColumnsToDraw);
        Describe();
    }

    private void Describe()
    {
        if (Chart is { } chart)
            Text = DetailsPresenter.DescribeChart(chart, Pointer, _timeProvider.GetUtcNow());
    }
}

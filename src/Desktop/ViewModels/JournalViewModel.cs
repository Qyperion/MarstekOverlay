using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;

using Microsoft.Extensions.Logging;

using System.IO;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>The tab of the details window with the outage journal: the totals over the latest periods and the
/// outages a month at a time. Kept current by <see cref="DetailsViewModel"/>.</summary>
public sealed partial class JournalViewModel : ObservableObject
{
    // With no outage going on, the journal changes only as the day, the week and the month move on.
    private static readonly TimeSpan ReadEvery = TimeSpan.FromMinutes(1);

    private readonly OutageJournal _outages;
    private readonly SampleHistory _history;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    private (Outage? Ongoing, Outage? LatestFinished) _journalState;
    private DateTimeOffset? _readAt;
    private CalendarMonth _currentMonth;
    private CalendarMonth _shownMonth;
    private CalendarMonth? _firstMonth;
    private IReadOnlyList<Outage> _shownOutages = [];
    private RecordedLoad _load = RecordedLoad.None;

    public JournalViewModel(OutageJournal outages, SampleHistory history, TimeProvider timeProvider, ILogger logger)
    {
        _outages = outages;
        _history = history;
        _timeProvider = timeProvider;
        _logger = logger;
        _currentMonth = _shownMonth = CalendarMonth.Of(timeProvider.GetUtcNow(), TimeZoneInfo.Local);
    }

    [ObservableProperty]
    public partial IReadOnlyList<OutageTotalsCard> Totals { get; private set; } = [];

    /// <summary>The outages of <see cref="MonthTitle"/>, the latest first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OutageEntry> Outages { get; private set; } = [];

    /// <summary>What the month listed adds up to; nothing for the current month, which <see cref="Totals"/>
    /// already tells, and for a month without outages.</summary>
    [ObservableProperty]
    public partial string? MonthTotal { get; private set; }

    /// <summary>The month of the journal that is listed.</summary>
    [ObservableProperty]
    public partial string MonthTitle { get; private set; } = string.Empty;

    /// <summary>How the latest export went; nothing until one is made.</summary>
    [ObservableProperty]
    public partial string? ExportStatus { get; private set; }

    public string ExportFileName => JournalExport.SuggestFileName(_shownMonth);

    private bool CanShowPreviousMonth => _firstMonth is { } first && _shownMonth > first;

    private bool CanShowNextMonth => _shownMonth < _currentMonth;

    /// <summary>Writes the outages of the month shown to <paramref name="path"/> and tells how that went.</summary>
    public void Export(string path)
    {
        try
        {
            var zone = TimeZoneInfo.Local;
            JournalExport.Save(path, OutagePart.AllWithin(_shownOutages, _shownMonth.StartsAt(zone), _shownMonth.EndsAt(zone), _load));
            ExportStatus = DetailsPresenter.DescribeExport(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "The outage journal was not exported to {Path}", path);
            ExportStatus = DetailsPresenter.DescribeExportFailure(exception);
        }
    }

    /// <summary>Reads the journal again when an outage has begun or ended, a month has turned over or a while
    /// has passed.</summary>
    public void Follow(DateTimeOffset now)
    {
        var month = CalendarMonth.Of(now, TimeZoneInfo.Local);
        if (month != _currentMonth)
        {
            // Whoever was looking at the current month goes on looking at the current one.
            if (_shownMonth == _currentMonth)
                _shownMonth = month;

            _currentMonth = month;
            _readAt = null;
        }

        var state = (_outages.Ongoing, _outages.LatestFinished);
        if (state == _journalState && now - _readAt < ReadEvery)
            return;

        _journalState = state;
        Read(now);
    }

    /// <summary>What was read may be gone; it is read again on the next <see cref="Follow"/>.</summary>
    public void ReadAnew() => _readAt = null;

    /// <summary>The journal is finished text, and so is the word on an export, which is not worth wording again.</summary>
    public void RefreshTexts()
    {
        ExportStatus = null;
        _readAt = null;
    }

    [RelayCommand(CanExecute = nameof(CanShowPreviousMonth))]
    private void ShowPreviousMonth() => ShowMonth(_shownMonth.Previous);

    [RelayCommand(CanExecute = nameof(CanShowNextMonth))]
    private void ShowNextMonth() => ShowMonth(_shownMonth.Next);

    private void ShowMonth(CalendarMonth month)
    {
        _shownMonth = month;
        ExportStatus = null;

        Read(_timeProvider.GetUtcNow());
    }

    private void Read(DateTimeOffset now)
    {
        var zone = TimeZoneInfo.Local;

        _readAt = now;
        _firstMonth = _outages.FirstStartedAt() is { } startedAt ? CalendarMonth.Of(startedAt, zone) : null;
        var (monthStart, monthEnd) = (_shownMonth.StartsAt(zone), _shownMonth.EndsAt(zone));
        _shownOutages = _outages.Between(monthStart, monthEnd);

        var latest = _outages.Since(DetailsPresenter.TotalsFrom(now));
        _load = _history.LoadAcross([.. _shownOutages, .. latest], [.. DetailsPresenter.TotalsBounds(now), monthStart, monthEnd]);

        var entries = DetailsPresenter.DescribeOutages(_shownOutages, _shownMonth, now);
        var totals = DetailsPresenter.DescribeTotals(latest, now, _load);

        // The same list is left in place, so that a row is not rebuilt under a tooltip open over it.
        if (!entries.SequenceEqual(Outages))
            Outages = entries;

        if (!totals.SequenceEqual(Totals))
            Totals = totals;

        MonthTotal = DetailsPresenter.DescribeMonthTotal(_shownOutages, _shownMonth, now, _load);
        MonthTitle = Phrases.Month(_shownMonth);
        ShowPreviousMonthCommand.NotifyCanExecuteChanged();
        ShowNextMonthCommand.NotifyCanExecuteChanged();
    }
}

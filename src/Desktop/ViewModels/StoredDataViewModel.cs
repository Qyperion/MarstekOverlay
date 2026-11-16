using CommunityToolkit.Mvvm.ComponentModel;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;

using Microsoft.Extensions.Logging;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>Deletes what the app has recorded — the samples, the outage journal, the log — all of it or all but
/// the latest while. Asking whether the user means it is left to the window.</summary>
public sealed partial class StoredDataViewModel(
    StationPipeline pipeline,
    DiagnosticLog log,
    TimeProvider timeProvider,
    ILogger<StoredDataViewModel> logger) : ObservableObject
{
    // The choice that deletes least.
    private KeptPeriod _kept = KeptPeriod.Month;

    public IReadOnlyList<ChoiceOption<KeptPeriod>> KeptPeriods => field ??= Enum.GetValues<KeptPeriod>()
        .Select(period => new ChoiceOption<KeptPeriod>(period, kept => kept.DisplayName, period == _kept, kept => _kept = kept))
        .ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool DeletesSamples { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool DeletesOutages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool DeletesLog { get; set; }

    /// <summary>A deletion is under way.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool IsDeleting { get; private set; }

    public bool CanDelete => !IsDeleting && (DeletesSamples || DeletesOutages || DeletesLog);

    /// <summary>The question to ask before <see cref="DeleteAsync"/>: what goes and what stays.</summary>
    public string Confirmation => StoredDataPresenter.AskToDelete(DeletesSamples, DeletesOutages, DeletesLog, _kept);

    /// <summary>How the latest deletion went; nothing until one is made.</summary>
    [ObservableProperty]
    public partial StatusMessage? Status { get; private set; }

    /// <summary>Expects to be called on the UI thread, which the records are deleted on. The log is rewritten on
    /// another one: a full log takes seconds.</summary>
    public async Task DeleteAsync()
    {
        var before = _kept.StartsAt(timeProvider.GetUtcNow());
        var deletesLog = DeletesLog;

        IsDeleting = true;
        Status = null;

        try
        {
            var deleted = pipeline.DeleteBefore(
                before,
                (DeletesSamples ? RecordKinds.Samples : RecordKinds.None) | (DeletesOutages ? RecordKinds.Outages : RecordKinds.None));
            var cleanup = deletesLog ? await Task.Run(() => log.DeleteBefore(before)) : null;

            if (cleanup?.Failure is { } failure)
                logger.LogWarning(failure, "The diagnostic log was not cleared, other files of it cleared {IsPartial}", cleanup.IsPartial);

            // After the log is cleared, so that what is left of it tells of this deletion.
            logger.LogInformation(
                "Stored data deleted on request, kept {Kept}: samples {Samples}, outages {Outages}, log {IsLogDeleted}",
                _kept,
                deleted.Samples ?? 0,
                deleted.Outages ?? 0,
                cleanup is { Failure: null });

            Status = StoredDataPresenter.DescribeDeletion(deleted, cleanup);
        }
        finally
        {
            IsDeleting = false;
        }
    }

    /// <summary>The names of the choices follow a new language; the word on a deletion is finished text, which
    /// is not worth wording again.</summary>
    public void RefreshTexts()
    {
        foreach (var period in KeptPeriods)
            period.RefreshName();

        Status = null;
    }
}

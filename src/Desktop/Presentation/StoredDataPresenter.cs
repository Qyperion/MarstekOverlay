using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>What the window that deletes stored data asks before it deletes and says once it has.</summary>
public static class StoredDataPresenter
{
    private const string Bullet = "• ";

    /// <summary>Names what is about to be deleted and what of it stays, since nothing brings it back.</summary>
    public static string AskToDelete(bool samples, bool outages, bool log, KeptPeriod kept) => Phrases.Lines(
        kept == KeptPeriod.Nothing
            ? Strings.StoredData_ConfirmAll
            : Phrases.Format(Strings.StoredData_ConfirmKeeping, kept.DisplayName),
        string.Empty,
        samples ? Bullet + Strings.StoredData_Samples : null,
        outages ? Bullet + Strings.StoredData_Outages : null,
        log ? Bullet + Strings.StoredData_Log : null,
        string.Empty,
        Strings.StoredData_CannotBeRestored);

    /// <param name="log">How clearing the log went; nothing when it was not asked to be.</param>
    public static StatusMessage DescribeDeletion(DeletedRecords records, LogCleanup? log) => new(
        Phrases.Sentences(
            records.Samples is { } samples ? Phrases.Format(Strings.StoredData_SamplesDeleted, samples) : null,
            records.Outages is { } outages ? Phrases.Format(Strings.StoredData_OutagesDeleted, outages) : null,
            log switch
            {
                null => null,
                { Failure: null } => Strings.StoredData_LogCleared,
                { IsPartial: true, Failure: { } failure } => Phrases.Format(Strings.StoredData_LogPartlyCleared, FailureReasons.OfFile(failure)),
                { Failure: { } failure } => Phrases.Format(Strings.StoredData_LogFailed, FailureReasons.OfFile(failure)),
            }),
        IsError: log?.Failure is not null);
}

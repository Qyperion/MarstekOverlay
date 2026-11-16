using MarstekOverlay.Desktop.Localization;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>What the user is told of an error the app does not get over, before it closes.</summary>
public static class FailureReport
{
    /// <param name="logPath">Where the log is written; null when the log is switched off.</param>
    public static string Describe(Exception exception, string? logPath) =>
        Phrases.Lines(
            Strings.Failure_Text,
            exception.Message,
            logPath is null ? null : Phrases.Format(Strings.Failure_Log, logPath));
}

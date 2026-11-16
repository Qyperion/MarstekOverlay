using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring;


namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Words what went wrong with a poll in the language of the app; the log keeps the English of the
/// exceptions.</summary>
public static class ErrorPresenter
{
    public static string Describe(PollError error) => error switch
    {
        UnansweredRequest unanswered => Phrases.Format(Strings.Error_NoAnswer, unanswered.Method, unanswered.WaitedFor.TotalSeconds),
        RefusedRequest refused => Phrases.Format(Strings.Error_Refused, refused.Method, refused.Reason, refused.Code),
        UnexpectedFailure failure => failure.Message,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "A kind of poll error with no wording."),
    };

    /// <returns>A line for each error; nothing after a clean poll.</returns>
    public static string? Describe(IReadOnlyList<PollError> errors) =>
        errors.Count > 0 ? Phrases.Lines([.. errors.Select(Describe)]) : null;
}

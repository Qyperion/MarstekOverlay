namespace MarstekOverlay.Desktop.Services;

/// <summary>How deleting the old entries of the log went.</summary>
/// <param name="Failure">Why a file of the log was left as it was; nothing when none was.</param>
/// <param name="IsPartial">Other files of the log were cleared all the same.</param>
public sealed record LogCleanup(Exception? Failure, bool IsPartial);

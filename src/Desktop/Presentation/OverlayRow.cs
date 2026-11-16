namespace MarstekOverlay.Desktop.Presentation;

/// <param name="Label">What <paramref name="Value"/> is a reading of. Nothing for a row worded as a whole.</param>
/// <param name="IsProminent">Drawn in the primary text style rather than as a secondary reading.</param>
public sealed record OverlayRow(OverlayRowKind Kind, string? Label, string Value, bool IsProminent = false, string? Hint = null);

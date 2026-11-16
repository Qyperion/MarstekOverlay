namespace MarstekOverlay.Desktop.Services;

/// <param name="IsChecked">Nothing for an entry that does something rather than switches it.</param>
/// <param name="Hint">What the entry does, where its name is not enough.</param>
public sealed record MenuEntry(string Text, Action Choose, bool? IsChecked = null, string? Hint = null);

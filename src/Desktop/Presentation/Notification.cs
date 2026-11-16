namespace MarstekOverlay.Desktop.Presentation;

/// <param name="Tone">How grave the news is; the channel picks its icon by it.</param>
public sealed record Notification(string Title, string Text, Tone Tone);

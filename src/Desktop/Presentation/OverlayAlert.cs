namespace MarstekOverlay.Desktop.Presentation;

/// <summary>A warning the panel keeps on show for as long as its cause lasts.</summary>
public sealed record OverlayAlert(string Text, Tone Tone);

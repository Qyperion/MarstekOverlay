namespace MarstekOverlay.Desktop.Presentation;

/// <summary>A finished line for the status line of a window.</summary>
public sealed record StatusMessage(string Text, bool IsError);

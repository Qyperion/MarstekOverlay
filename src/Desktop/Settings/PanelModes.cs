namespace MarstekOverlay.Desktop.Settings;

/// <summary>The switches of the panel that both its menu and the settings window turn. Whoever shows them takes
/// all of them apart at once, so a switch added here does not compile until every such place has room for it.</summary>
public readonly record struct PanelModes(bool IsTopmost, bool IsCompact, bool IsClickThrough);

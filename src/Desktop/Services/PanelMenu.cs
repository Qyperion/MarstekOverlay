using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Monitoring;

namespace MarstekOverlay.Desktop.Services;

/// <summary>What the menus of the panel and of the notification-area icon offer, so that the two cannot drift
/// apart. Asked for each time a menu opens: both the state behind it and the language may have changed since.</summary>
public sealed class PanelMenu(IShell shell, UserSettingsStore settings, StationMonitor monitor)
{
    /// <returns>The entries in groups, which a menu sets apart with a separator.</returns>
    public IReadOnlyList<IReadOnlyList<MenuEntry>> Groups()
    {
        var overlay = settings.Current.Overlay;

        return
        [
            [new MenuEntry(Strings.Overlay_Refresh, monitor.RequestRefresh)],
            [
                new MenuEntry(Strings.Overlay_Compact, () => Switch(panel => panel.IsCompact = !panel.IsCompact), overlay.IsCompact),
                new MenuEntry(Strings.Overlay_AlwaysOnTop, () => Switch(panel => panel.IsTopmost = !panel.IsTopmost), overlay.IsTopmost),
                new MenuEntry(
                    Strings.Overlay_ClickThrough,
                    () => Switch(panel => panel.IsClickThrough = !panel.IsClickThrough),
                    overlay.IsClickThrough,
                    Strings.Settings_ClickThroughHint),
            ],
            [
                new MenuEntry(Strings.Overlay_Details, shell.ShowDetails),
                new MenuEntry(Strings.Overlay_Settings, shell.ShowSettings),
            ],
            [new MenuEntry(Strings.Overlay_Exit, shell.Exit)],
        ];
    }

    private void Switch(Action<OverlaySettings> change) => settings.Change(current => change(current.Overlay));
}

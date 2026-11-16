using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Styling;

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MarstekOverlay.Desktop.Services;

/// <summary>The menu of the notification-area icon: what the panel's menu offers, led by showing or hiding the
/// panel. The items stay and are worded anew each time the menu opens: both the state behind them and the
/// language may have changed since. The menu offers the same entries in the same order every time.</summary>
public sealed class TrayMenu : ContextMenuStrip
{
    private readonly IShell _shell;
    private readonly PanelMenu _panelMenu;
    private readonly List<ToolStripMenuItem> _items = [];
    private IReadOnlyList<MenuEntry> _entries;

    public TrayMenu(IShell shell, PanelMenu panelMenu)
    {
        _shell = shell;
        _panelMenu = panelMenu;

        var groups = Groups();
        _entries = [.. groups.SelectMany(entries => entries)];

        foreach (var group in groups)
        {
            if (Items.Count > 0)
                Items.Add(new ToolStripSeparator());

            foreach (var index in Enumerable.Range(_items.Count, group.Count))
            {
                var item = new ToolStripMenuItem(string.Empty, null, (_, _) => _entries[index].Choose());

                _items.Add(item);
                Items.Add(item);
            }
        }

        new TrayMenuRenderer(MenuColors()).ApplyTo(this);
        Reword();
    }

    internal IReadOnlyList<ToolStripMenuItem> Entries => _items;

    internal void Reword()
    {
        _entries = [.. Groups().SelectMany(entries => entries)];

        foreach (var (item, entry) in _items.Zip(_entries))
            (item.Text, item.Checked) = (entry.Text, entry.IsChecked == true);
    }

    protected override void OnOpening(CancelEventArgs e)
    {
        Reword();
        base.OnOpening(e);
    }

    private IReadOnlyList<IReadOnlyList<MenuEntry>> Groups()
    {
        var toggleOverlay = new MenuEntry(_shell.IsOverlayVisible ? Strings.Tray_HidePanel : Strings.Tray_ShowPanel, _shell.ToggleOverlay);
        var panel = _panelMenu.Groups();

        return [[toggleOverlay, .. panel[0]], .. panel.Skip(1)];
    }

    private static TrayMenuColors MenuColors() => new(
        Background: Drawn(SurfacePalette.Field),
        Border: Drawn(SurfacePalette.FieldBorder),
        Highlight: Drawn(SurfacePalette.Accent),
        Text: TrayIcon.ColorOf(Tone.Primary),
        CheckMark: TrayIcon.ColorOf(Tone.Info));

    internal static Color Drawn(System.Windows.Media.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}

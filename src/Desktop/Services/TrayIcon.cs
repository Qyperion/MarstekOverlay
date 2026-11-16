using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Styling;

using Microsoft.Extensions.Logging;

using System.Drawing;
using System.Windows.Forms;

namespace MarstekOverlay.Desktop.Services;

/// <summary>The icon also carries the notifications: since Windows 10 a balloon tip of a notification-area icon
/// is shown as an ordinary notification, with nothing to register for it.</summary>
public sealed class TrayIcon : INotificationChannel, IStatusIndicator, IDisposable
{
    // NotifyIcon refuses a longer tooltip.
    private const int LongestTooltip = 127;

    private const string Ellipsis = "…";
    private const char LineBreak = '\n';

    // Windows has ignored the value since Vista and keeps a notification up for as long as its own settings say.
    private const int BalloonTimeoutMs = 0;

    private readonly ILogger<TrayIcon> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayMenu _menu;
    private TrayIconLook _look;
    private TrayIconImage _image;

    public TrayIcon(IShell shell, PanelMenu panelMenu, ILogger<TrayIcon> logger)
    {
        _logger = logger;
        _menu = new TrayMenu(shell, panelMenu);
        _look = TrayIconImage.LookOf(new OverlayState(), IconSize);
        _image = TrayIconImage.Draw(_look, ColorOf);
        _notifyIcon = new NotifyIcon
        {
            Icon = _image.Icon,
            Text = Strings.Common_ProductName,
            ContextMenuStrip = _menu,
        };
        _notifyIcon.DoubleClick += (_, _) => shell.ToggleOverlay();
        _notifyIcon.BalloonTipClicked += (_, _) => shell.ShowOverlay();
    }

    private static int IconSize => SystemInformation.SmallIconSize.Width;

    public void Show() => _notifyIcon.Visible = true;

    /// <summary>Takes the icon out of the notification area while the app still holds it.</summary>
    public void Hide() => _notifyIcon.Visible = false;

    public void Indicate(OverlayState state)
    {
        _notifyIcon.Text = Fit(state.Summary);

        var look = TrayIconImage.LookOf(state, IconSize);
        if (look == _look)
            return;

        var replaced = _image;
        _image = TrayIconImage.Draw(look, ColorOf);
        _look = look;
        _notifyIcon.Icon = _image.Icon;
        replaced.Dispose();

        _logger.LogDebug("Notification-area icon redrawn: {Look}", look);
    }

    public void Notify(Notification notification) =>
        _notifyIcon.ShowBalloonTip(
            BalloonTimeoutMs,
            notification.Title,
            notification.Text,
            notification.Tone switch
            {
                Tone.Critical => ToolTipIcon.Error,
                Tone.Warning => ToolTipIcon.Warning,
                _ => ToolTipIcon.Info,
            });

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _image.Dispose();
    }

    /// <summary>As many of the leading lines as the tooltip takes, under the name of the app when there is room
    /// for that as well.</summary>
    internal static string Fit(IReadOnlyList<string> summary)
    {
        var text = Join([Strings.Common_ProductName, .. summary]);

        for (var lines = summary.Count; text.Length > LongestTooltip && lines > 0; lines--)
            text = Join(summary.Take(lines));

        return text.Length > LongestTooltip
            ? string.Concat(text.AsSpan(0, LongestTooltip - Ellipsis.Length), Ellipsis)
            : text;

        static string Join(IEnumerable<string> lines) => string.Join(LineBreak, lines);
    }

    internal static Color ColorOf(Tone tone) => TrayMenu.Drawn(TonePalette.ColorOf(tone));
}

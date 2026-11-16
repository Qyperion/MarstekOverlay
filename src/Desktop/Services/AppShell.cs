using MarstekOverlay.Desktop.Views;

using Microsoft.Extensions.DependencyInjection;

using System.Windows;

namespace MarstekOverlay.Desktop.Services;

public sealed class AppShell(
    IServiceProvider services,
    UserSettingsStore settings,
    SettingsFollower follower) : IShell
{
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private DetailsWindow? _detailsWindow;

    public bool IsOverlayVisible => _overlay?.IsVisible == true;

    public void Start()
    {
        // Before any window exists, so that nothing is first drawn in one language and then redrawn in another.
        follower.FollowLanguage();

        // Resolved here rather than injected: the windows' view models depend on this shell.
        _overlay = services.GetRequiredService<OverlayWindow>();
        if (!settings.Current.Overlay.IsHiddenAtStartup)
            _overlay.Show();

        services.GetRequiredService<TrayIcon>().Show();
        services.GetRequiredService<AlertNotifier>().Start();

        follower.FollowStation();

        if (follower.Station is null)
            ShowSettings();
    }

    public void ToggleOverlay()
    {
        if (_overlay is not { } overlay)
            return;

        if (overlay.IsVisible)
            overlay.Hide();
        else
            overlay.Show();
    }

    public void ShowOverlay() => _overlay?.ShowOnTop();

    public void HideOverlay() => _overlay?.Hide();

    public void ShowSettings()
    {
        if (_settingsWindow is { } open)
        {
            open.BringForward();
            return;
        }

        _settingsWindow = Open<SettingsWindow>(() =>
        {
            _settingsWindow = null;
            settings.Save();
        });
    }

    public void ShowDetails()
    {
        if (_detailsWindow is { } open)
        {
            open.BringForward();
            return;
        }

        _detailsWindow = Open<DetailsWindow>(() => _detailsWindow = null);
    }

    public void Exit()
    {
        settings.Save();
        Application.Current.Shutdown();
    }

    /// <summary>Opens a window in a scope of its own, which takes the window's view models with it once the window
    /// has closed.</summary>
    private TWindow Open<TWindow>(Action closed)
        where TWindow : Window
    {
        var scope = services.CreateScope();

        TWindow window;
        try
        {
            window = scope.ServiceProvider.GetRequiredService<TWindow>();
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        window.Closed += (_, _) =>
        {
            closed();
            scope.Dispose();
        };
        window.Show();

        return window;
    }
}

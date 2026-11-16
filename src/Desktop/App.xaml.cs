using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.StartupConfiguration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Windows;
using System.Windows.Threading;

namespace MarstekOverlay.Desktop;

public partial class App : Application
{
    // Only to take the icon out of the notification area; a UI thread that is stuck must not keep the process up.
    private static readonly TimeSpan LongestIconRemoval = TimeSpan.FromSeconds(2);

    private SingleInstance? _instance;
    private IHost? _host;
    private TrayIcon? _trayIcon;
    private bool _isFailing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var builder = AppHost.CreateBuilder(e.Args);

            // Before anything takes hold of the data folder: a copy already running there is only asked to show
            // its panel.
            _instance = SingleInstance.Claim(AppHost.StorageOf(builder).RootDirectory);
            if (_instance is null)
            {
                Shutdown();
                return;
            }

            var uiContext = new DispatcherSynchronizationContext(Dispatcher);
            _host = AppHost.Create(builder, uiContext);
            WatchForFailures(_host.Services.GetRequiredService<ILogger<App>>(), _host.Services.GetRequiredService<DiagnosticLog>());

            _host.Start();
            var shell = _host.Services.GetRequiredService<AppShell>();
            shell.Start();
            _trayIcon = _host.Services.GetRequiredService<TrayIcon>();
            _instance.WhenAskedToShow(uiContext, shell.ShowOverlay);
#if DEBUG
            FailureDrill.Schedule(_host.Services, Dispatcher);
#endif
        }
        catch (Exception exception)
        {
            // Nothing is on screen yet, so a broken appsettings.json would otherwise look like the app never started.
            MessageBox.Show(exception.Message, Strings.Common_ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(exitCode: 1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.AnnounceExit();

        using (_host)
            _host?.StopAsync().GetAwaiter().GetResult();

        // Only once the database is closed, or a copy started meanwhile would find it held.
        _instance?.Dispose();

        base.OnExit(e);
    }

    private void WatchForFailures(ILogger<App> logger, DiagnosticLog log)
    {
        DispatcherUnhandledException += (_, failure) =>
        {
            logger.LogCritical(failure.Exception, "Unhandled exception on the UI thread");
            failure.Handled = true;
            Fail(failure.Exception, log.FilePath);
        };

        // Nothing keeps the process from ending after such an exception, and OnExit does not run: it is at least
        // written down and told, and the icon is taken out of the notification area, where it would otherwise stay
        // until the mouse passes over it.
        AppDomain.CurrentDomain.UnhandledException += (_, failure) =>
        {
            if (failure.ExceptionObject is not Exception exception)
                return;

            logger.LogCritical(exception, "Unhandled exception on a background thread");
            HideTrayIcon(logger);
            MessageBox.Show(FailureReport.Describe(exception, log.FilePath), Strings.Common_ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, failure) =>
        {
            logger.LogError(failure.Exception, "Unobserved exception of a task");
            failure.SetObserved();
        };
    }

    private void HideTrayIcon(ILogger<App> logger)
    {
        if (_trayIcon is not { } icon)
            return;

        try
        {
            Dispatcher.Invoke(icon.Hide, DispatcherPriority.Send, CancellationToken.None, LongestIconRemoval);
        }
        catch (Exception exception)
        {
            // The process is ending anyway; what failed here must not keep the failure from being told.
            logger.LogWarning(exception, "The notification-area icon was not taken out");
        }
    }

    // What the app holds may be left in a state nothing can be trusted of, so it does not carry on. It closes the
    // regular way, which takes its icon out of the notification area and closes the database.
    private void Fail(Exception exception, string? logPath)
    {
        // The message box keeps the dispatcher going, and whatever fails next while it is up is only logged.
        if (_isFailing)
            return;

        _isFailing = true;
        MessageBox.Show(FailureReport.Describe(exception, logPath), Strings.Common_ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(exitCode: 1);
    }
}

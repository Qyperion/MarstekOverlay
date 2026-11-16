#if DEBUG
using MarstekOverlay.Storage;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Windows.Threading;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Has the running app fail on request, to see on it what an unexpected failure does. Asked for on the
/// command line, <c>--Debug:FailOn=Ui|Background|Database --Debug:FailAfterSeconds=n</c>; a Release build has
/// none of it.</summary>
internal static class FailureDrill
{
    private const string Section = "Debug";
    private const int DefaultDelaySeconds = 10;

    public static void Schedule(IServiceProvider services, Dispatcher dispatcher)
    {
        var drill = services.GetRequiredService<IConfiguration>().GetSection(Section);
        if (drill.GetValue<FailurePoint?>("FailOn") is not { } point)
            return;

        var delay = TimeSpan.FromSeconds(drill.GetValue("FailAfterSeconds", DefaultDelaySeconds));
        var logger = services.GetRequiredService<ILogger<App>>();
        logger.LogWarning("Failure drill: the app fails at {Point} in {Delay}", point, delay);

        _ = Task.Delay(delay).ContinueWith(_ => Fail(point, services, dispatcher), TaskScheduler.Default);
    }

    private static void Fail(FailurePoint point, IServiceProvider services, Dispatcher dispatcher)
    {
        switch (point)
        {
            case FailurePoint.Ui:
                dispatcher.BeginInvoke(() => throw new InvalidOperationException("Failure drill on the UI thread"));
                break;
            case FailurePoint.Background:
                ThreadPool.QueueUserWorkItem(_ => throw new InvalidOperationException("Failure drill on a thread-pool thread"));
                break;
            case FailurePoint.Database:
                dispatcher.BeginInvoke(() => services.GetRequiredService<HistoryDatabase>().Dispose());
                break;
        }
    }

    private enum FailurePoint
    {
        Ui,
        Background,
        Database,
    }
}
#endif

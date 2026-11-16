using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>A thread of the kind view models are made on in the app: single-threaded apartment with a dispatcher
/// of its own, which their timers are tied to. The dispatcher is not pumped, so a timer never ticks by itself.</summary>
internal static class UiThread
{
    /// <summary>Runs <paramref name="test"/> on a fresh such thread, in the regional format the test has set.</summary>
    public static void Run(Action test)
    {
        var format = CultureInfo.CurrentCulture;
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = format;
            try
            {
                test();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }
}

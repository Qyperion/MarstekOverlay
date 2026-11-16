using System.Windows.Threading;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>Ticks once a second on the dispatcher of the thread it was made on, below input and rendering:
/// between two answers of the station, the ages of the readings and the durations still move on to the second.
/// Stopped until started.</summary>
public sealed class SecondsClock
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background, Dispatcher.CurrentDispatcher) { Interval = Step };

    public SecondsClock(Action tick) => _timer.Tick += (_, _) => tick();

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}

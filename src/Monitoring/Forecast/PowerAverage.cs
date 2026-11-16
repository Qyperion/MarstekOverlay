using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Forecast;

/// <summary>The power of one steady flow averaged over a window of the latest samples. A sample of another
/// flow starts the averaging over.</summary>
internal sealed class PowerAverage(TimeSpan window)
{
    // About a minute of answers settles an average: two polls at the usual interval of 30 seconds, more at a
    // shorter one. An answer that comes again within seconds is the same reading rather than a sample.
    private static readonly TimeSpan MinSampleSpacing = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MinObservedSpan = TimeSpan.FromSeconds(55);
    private const int MinSampleCount = 3;

    private readonly List<(DateTimeOffset At, Power Power)> _samples = [];

    private ForecastStatus? _flow;

    /// <param name="flow">What <paramref name="power"/> is the power of.</param>
    /// <returns>Nothing until enough samples of this flow are in.</returns>
    public Power? Add(DateTimeOffset now, ForecastStatus flow, Power power)
    {
        if (flow != _flow)
            _samples.Clear();

        _flow = flow;
        _samples.RemoveAll(sample => now - sample.At > window);
        if (_samples.Count == 0 || now - _samples[^1].At >= MinSampleSpacing)
            _samples.Add((now, power));

        if (_samples.Count < MinSampleCount || now - _samples[0].At < MinObservedSpan)
            return null;

        return new Power(_samples.Average(sample => sample.Power.Watts));
    }

    public void Clear()
    {
        _samples.Clear();
        _flow = null;
    }
}

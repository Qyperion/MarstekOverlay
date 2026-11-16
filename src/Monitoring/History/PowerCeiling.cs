using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.History;

/// <summary>The top of a power scale: a round value somewhat above the highest reading, so that the scale does
/// not shift with every sample and the highest reading stays clear of the edge of the chart.</summary>
internal static class PowerCeiling
{
    public static readonly Power Lowest = new(100);

    private const double Headroom = 1.2;

    private static readonly double[] Steps = [1, 1.5, 2, 3, 4, 5, 6, 8, 10];

    public static Power Above(double highestWatts)
    {
        var watts = highestWatts * Headroom;
        if (watts <= Lowest.Watts)
            return Lowest;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(watts)));

        return new Power(magnitude * Steps.First(step => step * magnitude >= watts));
    }
}

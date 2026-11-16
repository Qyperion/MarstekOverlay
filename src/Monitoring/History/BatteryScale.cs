using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.History;

/// <summary>The scale the battery power is drawn in: charging above zero and discharging below it, each side
/// reaching as far as the power went that way over the period. The station charges at about ten times the
/// power an ordinary load draws, so one reach for both sides would press the discharge flat against zero
/// whenever no charging is in sight.</summary>
/// <param name="Floor">Negative: the deepest discharge the scale holds.</param>
public readonly record struct BatteryScale(Power Floor, Power Ceiling)
{
    public static readonly BatteryScale Narrowest = new(-PowerCeiling.Lowest, PowerCeiling.Lowest);

    /// <param name="watts">Positive while charging.</param>
    /// <returns>The narrowest scale when there is nothing to fit it to.</returns>
    public static BatteryScale Around(IReadOnlyCollection<double> watts) =>
        watts.Count == 0
            ? Narrowest
            : new BatteryScale(-PowerCeiling.Above(-watts.Min()), PowerCeiling.Above(watts.Max()));
}

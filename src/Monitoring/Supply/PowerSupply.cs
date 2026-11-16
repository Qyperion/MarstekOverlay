namespace MarstekOverlay.Monitoring.Supply;

/// <param name="Since">When the source was first seen; nothing while the source is unknown.</param>
/// <param name="IsSinceObserved">False when nobody was watching at the switch, so the source may have been the
/// same for longer than <paramref name="Since"/> tells.</param>
public readonly record struct PowerSupply(PowerSource Source, DateTimeOffset? Since = null, bool IsSinceObserved = false)
{
    public static readonly PowerSupply Unknown = new(PowerSource.Unknown);
}

namespace MarstekOverlay.Monitoring.Supply;

/// <param name="IsWatched">Whether the app was following the source when it changed, as opposed to making it out
/// anew after a start or a long gap. A watched change may still have begun unseen: see
/// <see cref="PowerSupply.IsSinceObserved"/>.</param>
public sealed record PowerSourceChange(PowerSupply Previous, PowerSupply Current, bool IsWatched);

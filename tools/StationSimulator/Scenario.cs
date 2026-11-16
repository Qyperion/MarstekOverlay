namespace MarstekOverlay.StationSimulator;

/// <summary>The state the simulated station starts in.</summary>
public enum Scenario
{
    /// <summary>No mains: the battery carries the load.</summary>
    Battery,

    /// <summary>Mains, battery not full: the grid charges it, then holds it full.</summary>
    Charging,

    /// <summary>Mains, battery full: the grid passes to the backup output unseen, with a top-up now and then.</summary>
    Full,

    /// <summary>As <see cref="Full"/>, with a top-up every minute instead of every 17.</summary>
    TopUp,
}

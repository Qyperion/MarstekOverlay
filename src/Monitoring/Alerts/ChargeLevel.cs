namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Where the charge stands among the thresholds of <see cref="AlertSettings"/>.</summary>
public enum ChargeLevel
{
    /// <summary>Above every threshold.</summary>
    Comfortable,

    Low,

    /// <summary>At or below the lowest threshold.</summary>
    Critical,
}

namespace MarstekOverlay.Monitoring.Alerts;

public class AlertSettings
{
    public const double MinChargeThresholdPercent = 1;
    public const double MaxChargeThresholdPercent = 100;
    public const double MinTemperatureLimitCelsius = -40;
    public const double MaxTemperatureLimitCelsius = 100;
    public const double MinStaleAfterMinutes = 1;
    public const double MaxStaleAfterMinutes = 1440;

    /// <summary>Kinds nobody wants to hear about. Listing these rather than the wanted ones leaves a kind
    /// added later switched on.</summary>
    public List<AlertKind> DisabledKinds { get; set; } = [];

    /// <summary>Each one is worth an alert when the charge comes down to it; the lowest is the critical one.</summary>
    public List<double> ChargeThresholdsPercent { get; set; } = [50, 30, 15];

    public double MinBatteryTemperatureCelsius { get; set; } = 5;
    public double MaxBatteryTemperatureCelsius { get; set; } = 45;

    /// <summary>How long the station may stay silent before that is worth an alert.</summary>
    public double StaleAfterMinutes { get; set; } = 5;

    public bool Watches(AlertKind kind) => !DisabledKinds.Contains(kind);

    public void Watch(AlertKind kind, bool isWatched)
    {
        DisabledKinds.Remove(kind);

        if (!isWatched)
            DisabledKinds.Add(kind);
    }

    /// <summary>The thresholds as they are used, lowest first: a file edited by hand may hold anything.</summary>
    public IReadOnlyList<double> UsableChargeThresholds() =>
    [
        .. ChargeThresholdsPercent
            .Where(threshold => threshold is >= MinChargeThresholdPercent and <= MaxChargeThresholdPercent)
            .Distinct()
            .Order(),
    ];

    public ChargeLevel ChargeLevelAt(double socPercent)
    {
        var thresholds = UsableChargeThresholds();

        if (thresholds.Count == 0 || socPercent > thresholds[^1])
            return ChargeLevel.Comfortable;

        return socPercent <= thresholds[0] ? ChargeLevel.Critical : ChargeLevel.Low;
    }
}

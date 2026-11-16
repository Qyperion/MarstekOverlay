namespace MarstekOverlay.StationSimulator;

/// <summary>The battery and the power flows of a Venus E 3.0 used as an uninterruptible supply, with the figures
/// measured in <c>docs/StationBehaviour.md</c>. Thread-safe: the requests and the timeline move it from two
/// threads.</summary>
public sealed class SimulatedStation(SimulatorOptions options, DateTimeOffset startedAt)
{
    public const double CapacityWattHours = 5120;

    // What the battery side draws, top-ups included; the grid port shows it plus the load, about 2.5 kW in all.
    private const double ChargingWatts = 2200;
    private const double ChargeEfficiency = 0.97;
    private const double DischargeEfficiency = 0.92;
    private const double FullSocPercent = 100;

    // A top-up restores about 15 Wh, so the charge settles this far short of full; the API rounds it down to 99%.
    private const double SettledSocPercent = 99.7;

    private const double LoadWaver = 15;

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LoadWaverPeriod = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();

    private double _socPercent = options.SocPercent;
    private bool _isMainsPresent = options.IsMainsPresentAtStart;
    private double _gridInputWattHours = 17000;
    private DateTimeOffset _updatedAt = startedAt;
    private DateTimeOffset _fullSince = startedAt;
    private DateTimeOffset? _settledAt;

    public DateTimeOffset StartedAt { get; } = startedAt;

    public StationReading Read(DateTimeOffset now)
    {
        lock (_lock)
        {
            MoveTo(now);
            return Describe(now);
        }
    }

    public void SwitchMains(bool isPresent, DateTimeOffset now)
    {
        lock (_lock)
        {
            MoveTo(now);
            _isMainsPresent = isPresent;
            _settledAt = null;
            _fullSince = now;
        }
    }

    private void MoveTo(DateTimeOffset now)
    {
        while (_updatedAt < now)
        {
            var step = now - _updatedAt < Step ? now - _updatedAt : Step;
            _updatedAt += step;
            Advance(step, _updatedAt);
        }
    }

    private void Advance(TimeSpan step, DateTimeOffset at)
    {
        var hours = step.TotalHours * options.Speed;
        var reading = Describe(at);

        if (reading.ChargingWatts > 0)
        {
            _socPercent = Math.Min(FullSocPercent, _socPercent + ToPercent(reading.ChargingWatts * ChargeEfficiency * hours));
            _gridInputWattHours += (reading.ChargingWatts + reading.LoadWatts) * hours;

            if (_socPercent >= FullSocPercent)
            {
                _fullSince = at;
                _settledAt = null;
            }

            return;
        }

        if (!_isMainsPresent)
        {
            _socPercent = Math.Max(0, _socPercent - ToPercent(reading.LoadWatts / DischargeEfficiency * hours));
            return;
        }

        if (_socPercent >= FullSocPercent && at - _fullSince >= options.TopUpEvery)
        {
            _socPercent = SettledSocPercent;
            _settledAt = at;
        }
    }

    private StationReading Describe(DateTimeOffset at)
    {
        var load = _socPercent > 0 || _isMainsPresent ? LoadAt(at) : 0;
        var isWaitingToTopUp = _settledAt is { } settledAt && at - settledAt < options.TopUpDelay;
        var charging = _isMainsPresent && _socPercent < FullSocPercent && !isWaitingToTopUp ? ChargingWatts : 0;

        return new StationReading(
            _socPercent,
            _isMainsPresent,
            OngridWatts: charging > 0 ? -(charging + load) : 0,
            LoadWatts: load,
            ChargingWatts: charging,
            Math.Round(_gridInputWattHours),
            options.TemperatureCelsius);
    }

    private double LoadAt(DateTimeOffset at) =>
        Math.Max(0, Math.Round(options.LoadWatts + LoadWaver * Math.Sin(2 * Math.PI * ((at - StartedAt) / LoadWaverPeriod))));

    private static double ToPercent(double wattHours) => wattHours / CapacityWattHours * 100;
}

/// <param name="SocPercent">The precise charge; the API rounds it down.</param>
/// <param name="OngridWatts">Negative while the station draws from the grid port.</param>
public sealed record StationReading(
    double SocPercent,
    bool IsMainsPresent,
    double OngridWatts,
    double LoadWatts,
    double ChargingWatts,
    double GridInputWattHours,
    double TemperatureCelsius);

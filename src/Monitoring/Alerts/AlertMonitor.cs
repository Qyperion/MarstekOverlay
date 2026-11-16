using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

using Microsoft.Extensions.Options;

namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Decides what is worth telling the user about. An alert is raised once, when its condition sets in,
/// and not again until the condition has cleared with a margin to spare. A condition that already held when
/// the watch began is not news and is passed over, unless it is critical. A switch of the power source is
/// news only when it was watched, which leaves out the source being found after a long silence. The one
/// exception is the station found on the battery while the source was not known, after a start or a gap in the
/// answers: nobody watched the mains go, and whoever is at the computer has not been told.</summary>
public sealed class AlertMonitor
{
    // Without the margin, a reading that hovers at a limit would raise the same alert over and over.
    private const double TemperatureRearmMarginCelsius = 1;

    private readonly PowerSourceTracker _tracker;
    private readonly TimeSpan _batteryAnswerTrustedFor;
    private readonly Queue<PowerSourceChange> _sourceChanges = new();
    private readonly ChargeThresholdWatch _charge = new();
    private readonly SilenceWatch _silence;
    private readonly Latch _temperature = new();
    private readonly Latch _chargingForbidden = new();
    private readonly Latch _dischargingForbidden = new();

    private bool _isSourceKnown;

    public AlertMonitor(PowerSourceTracker tracker, IOptions<PollingSettings> polling)
    {
        _tracker = tracker;
        _batteryAnswerTrustedFor = ReadingFreshness.TrustedFor(MarstekMethods.BatteryGetStatus, polling.Value);
        _silence = new SilenceWatch(polling.Value);

        tracker.SourceChanged += _sourceChanges.Enqueue;
    }

    /// <summary>Expects the tracker and the outage journal to have seen the same snapshot already.</summary>
    /// <param name="latestOutage">The latest finished outage, told along with the mains coming back.</param>
    public AlertReport Update(StationSnapshot snapshot, Outage? latestOutage, AlertSettings settings, DateTimeOffset now)
    {
        var findings = new AlertFindings();
        var battery = RecentBatteryAnswer(snapshot, now);

        TellOfSourceChanges(snapshot, latestOutage, settings, findings);
        TellOfOutageFoundUnwatched(snapshot, settings, findings);

        if (Watches(settings, AlertKind.LowCharge, _charge))
            _charge.Follow(snapshot, settings, _tracker.Supply.Source, findings);

        if (Watches(settings, AlertKind.BatteryTemperature, _temperature))
            WatchTemperature(battery, settings, findings);

        if (Watches(settings, AlertKind.FlowForbidden, _dischargingForbidden, _chargingForbidden))
            WatchFlowPermissions(battery, findings);

        _silence.Listen(snapshot, settings, now);
        if (Watches(settings, AlertKind.StaleData, _silence))
            _silence.Follow(settings, now, findings);

        return findings.ToReport();
    }

    // A kind switched off forgets what it has told of: switched on again, it starts as if the watch had just begun.
    private static bool Watches(AlertSettings settings, AlertKind kind, params ReadOnlySpan<IAlertWatch> watches)
    {
        if (settings.Watches(kind))
            return true;

        foreach (var watch in watches)
            watch.Forget();

        return false;
    }

    private void TellOfSourceChanges(StationSnapshot snapshot, Outage? latestOutage, AlertSettings settings, AlertFindings findings)
    {
        var soc = snapshot.EnergySystem?.Value.BatterySocPercent;

        while (_sourceChanges.TryDequeue(out var change))
        {
            if (!change.IsWatched)
                continue;

            Alert? alert = change.Current.Source switch
            {
                PowerSource.Battery => new MainsLostAlert(soc),
                PowerSource.Mains => new MainsRestoredAlert(
                    latestOutage is { IsOngoing: false } outage && outage.StartedAt == change.Previous.Since ? outage : null,
                    soc),
                _ => null,
            };

            if (alert is not null && settings.Watches(alert.Kind))
                findings.Raised.Add(alert);
        }
    }

    // A source made out while it was not known: the first in this run, or the first after the computer slept or
    // the station fell silent for long. Nobody watched an outage found so begin, yet it is news to whoever is at
    // the computer, an outage carried over from the run before or from before the gap included.
    private void TellOfOutageFoundUnwatched(StationSnapshot snapshot, AlertSettings settings, AlertFindings findings)
    {
        if (snapshot.EnergySystem is not { } reading)
            return;

        var source = _tracker.Supply.Source;
        var isJustFound = !_isSourceKnown && source != PowerSource.Unknown;
        _isSourceKnown = source != PowerSource.Unknown;

        if (isJustFound && source == PowerSource.Battery && settings.Watches(AlertKind.MainsLost))
            findings.Raised.Add(new MainsLostAlert(reading.Value.BatterySocPercent, IsSwitchSeen: false));
    }

    // An answer of long ago tells nothing of the battery now, and a warning made of it would never go away.
    private BatteryStatus? RecentBatteryAnswer(StationSnapshot snapshot, DateTimeOffset now) =>
        snapshot.Battery is { } answer && now - answer.ReceivedAt <= _batteryAnswerTrustedFor ? answer.Value : null;

    private void WatchTemperature(BatteryStatus? battery, AlertSettings settings, AlertFindings findings)
    {
        if (battery?.TemperatureCelsius is not { } celsius)
            return;

        var (min, max) = (settings.MinBatteryTemperatureCelsius, settings.MaxBatteryTemperatureCelsius);
        double? limitCrossed = celsius > max ? max : celsius < min ? min : null;
        var isWellWithin = celsius >= min + TemperatureRearmMarginCelsius && celsius <= max - TemperatureRearmMarginCelsius;

        findings.Follow(
            _temperature,
            limitCrossed is { } limit ? new BatteryTemperatureAlert(celsius, limit) : null,
            isCleared: isWellWithin);
    }

    private void WatchFlowPermissions(BatteryStatus? battery, AlertFindings findings)
    {
        if (battery is null)
            return;

        WatchPermission(_dischargingForbidden, BatteryFlow.Discharging, battery.IsDischargingAllowed, findings);
        WatchPermission(_chargingForbidden, BatteryFlow.Charging, battery.IsChargingAllowed, findings);
    }

    private static void WatchPermission(Latch latch, BatteryFlow flow, bool? isAllowed, AlertFindings findings)
    {
        if (isAllowed is { } allowed)
            findings.Follow(latch, allowed ? null : new FlowForbiddenAlert(flow), isCleared: allowed);
    }
}

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Desktop.Presentation;

public static class OverlayPresenter
{
    private const double EmptySocPercent = 0;

    public static OverlayState SettingsRequired() => new()
    {
        StateText = Strings.Overlay_SettingsRequired,
        StateTone = Tone.Warning,
        CompactText = Strings.Overlay_SettingsRequired,
        CompactTone = Tone.Warning,
        Summary = [Strings.Overlay_SettingsRequired],
    };

    public static OverlayState Present(
        StationState state,
        ForecastSettings forecastSettings,
        AlertSettings alertSettings,
        OverlaySettings overlay,
        PollingSettings polling,
        HistoryChart? chart,
        DateTimeOffset now)
    {
        var reading = state.Snapshot.EnergySystem;
        var isFresh = now - reading?.ReceivedAt <= ReadingFreshness.TrustedFor(MarstekMethods.EnergySystemGetStatus, polling);
        var warnings = state.Alerts.Select(AlertPresenter.DescribeOnPanel).OfType<OverlayAlert>().ToList();
        var shown = new OverlayState
        {
            CompactAlert = Combine(warnings),
            Chart = chart is null ? null : new OverlayChart(chart, DescribeChart(chart)),
            Alerts = warnings,
            Rows = [.. DescribeRows(state, forecastSettings, polling, isFresh, now).Where(row => overlay.Shows(row.Kind))],
            ErrorHint = ErrorPresenter.Describe(state.Snapshot.Errors),
        };

        if (reading is null)
            return Waiting(shown);

        var charged = WithCharge(shown, reading.Value, alertSettings);

        return isFresh ? Live(charged, state, reading, forecastSettings, now) : Stale(charged, reading.ReceivedAt);
    }

    /// <summary>The lowest, the average and the highest load of the period a chart covers.</summary>
    public static string SummarizeLoad(HistoryChart chart)
    {
        var period = Phrases.Period(chart.To - chart.From);

        return chart.Summary is { } load
            ? Phrases.Format(Strings.Chart_Summary, period, Units.Format(load.Min), Units.Format(load.Average), Units.Format(load.Max))
            : Phrases.Format(Strings.Chart_NoSamples, period);
    }

    // No answer yet: either none has been asked for long enough, or the asking has already failed.
    private static OverlayState Waiting(OverlayState shown)
    {
        var hasFailed = shown.ErrorHint is not null;
        var (text, tone) = hasFailed
            ? (Strings.Overlay_WaitingForFirstAnswer, Tone.Warning)
            : (Strings.Overlay_WaitingForData, Tone.Secondary);

        return shown with
        {
            StateText = text,
            StateTone = tone,
            Supply = hasFailed ? SupplyMark.NoConnection : SupplyMark.None,
            CompactText = text,
            CompactTone = tone,
            Summary = [text],
            StaleText = hasFailed ? Strings.Overlay_NoDataYet : string.Empty,
        };
    }

    private static OverlayState WithCharge(OverlayState shown, EnergySystemStatus status, AlertSettings alertSettings) => shown with
    {
        SocText = status.BatterySocPercent is { } soc ? Units.Percent(Math.Round(soc)) : OverlayState.UnknownSocText,
        SocPercent = Math.Clamp(status.BatterySocPercent ?? EmptySocPercent, EmptySocPercent, Percent.Whole),
        SocTone = status.BatterySocPercent is { } charge ? alertSettings.ChargeLevelAt(charge).Tone : Tone.Secondary,
    };

    private static OverlayState Stale(OverlayState shown, DateTimeOffset answeredAt)
    {
        var staleText = Phrases.Format(Strings.Overlay_Stale, Phrases.TimeOfAnswer(answeredAt));

        return shown with
        {
            StateText = Strings.Overlay_NoFreshData,
            StateTone = Tone.Warning,
            Supply = SupplyMark.NoConnection,
            CompactText = Strings.Overlay_NoFreshData,
            CompactTone = Tone.Warning,
            CompactHint = Phrases.Lines(Strings.Overlay_NoFreshData, staleText),
            Summary = [Strings.Overlay_NoFreshData, staleText],
            StaleText = staleText,
        };
    }

    private static OverlayState Live(
        OverlayState shown,
        StationState state,
        Reading<EnergySystemStatus> reading,
        ForecastSettings forecastSettings,
        DateTimeOffset now)
    {
        var (supply, status) = (state.Supply, reading.Value);

        // An answer that came on time has no line of its own; when it came is told here for whoever asks.
        var (stateText, stateTone, sinceHint) = DescribeSupply(supply, status, now);
        var stateHint = Phrases.Sentences(sinceHint, Phrases.AnsweredAt(reading.ReceivedAt));

        return shown with
        {
            StateText = stateText,
            StateTone = stateTone,
            StateHint = stateHint,
            Supply = supply.Source switch
            {
                PowerSource.Mains => SupplyMark.OnMains,
                PowerSource.Battery => SupplyMark.OnBattery,
                _ => SupplyMark.None,
            },
            CompactText = Phrases.Readings(
                status.OffgridPower is { } load ? Units.Format(load) : null,
                ForecastPresenter.DescribeBriefly(state.Forecast, now)),
            CompactHint = Phrases.Lines(stateText, stateHint),
            Summary =
            [
                Phrases.Readings(stateText, shown.SocText),
                Phrases.Labelled(Strings.Overlay_BackupOutput, DescribePower(status.OffgridPower)),
                ForecastPresenter.Describe(state.Forecast, forecastSettings, now),
            ],
        };
    }

    private static OverlayAlert? Combine(IReadOnlyList<OverlayAlert> warnings) =>
        warnings.Count == 0
            ? null
            : new OverlayAlert(
                Phrases.Lines([.. warnings.Select(warning => warning.Text)]),
                warnings.Any(warning => warning.Tone == Tone.Critical) ? Tone.Critical : Tone.Warning);

    private static IEnumerable<OverlayRow> DescribeRows(
        StationState state,
        ForecastSettings forecastSettings,
        PollingSettings polling,
        bool isFresh,
        DateTimeOffset now)
    {
        var (snapshot, supply, ongoingOutage, latestOutage, forecast, _) = state;
        var status = snapshot.EnergySystem?.Value;

        // The heading tells when the power flows went stale; an answer to another request has no such place,
        // and one of long ago would pass for the present.
        Reading<T>? Recent<T>(Reading<T>? reading, string method) =>
            now - reading?.ReceivedAt <= ReadingFreshness.TrustedFor(method, polling) ? reading : null;

        return
        [
            .. DescribeForecast(supply, forecast, forecastSettings, now),
            new OverlayRow(OverlayRowKind.Battery, Strings.Overlay_Battery, DescribeBatteryPower(status, supply), IsProminent: true),
            new OverlayRow(
                OverlayRowKind.StoredEnergy,
                Strings.Overlay_StoredEnergy,
                Describe(snapshot.EnergySystem, _ => forecast.StoredEnergy, Units.Format)),
            OutagePhrases.DescribeCurrent(snapshot, ongoingOutage, isFresh, now),
            OutagePhrases.DescribeLatest(latestOutage, now),
            new OverlayRow(OverlayRowKind.Grid, Strings.Overlay_Grid, DescribePower(status?.OngridPower)),
            new OverlayRow(OverlayRowKind.BackupOutput, Strings.Overlay_BackupOutput, DescribePower(status?.OffgridPower)),
            new OverlayRow(OverlayRowKind.Solar, Strings.Overlay_Solar, DescribePower(status?.PvPower)),
            new OverlayRow(
                OverlayRowKind.BatteryTemperature,
                Strings.Overlay_BatteryTemperature,
                Describe(Recent(snapshot.Battery, MarstekMethods.BatteryGetStatus), battery => battery.TemperatureCelsius, Units.Celsius)),
            new OverlayRow(
                OverlayRowKind.WifiSignal,
                Strings.Overlay_WifiSignal,
                Describe(Recent(snapshot.Wifi, MarstekMethods.WifiGetStatus), wifi => wifi.SignalStrengthDbm, Units.Dbm)),
            new OverlayRow(
                OverlayRowKind.OperatingMode,
                Strings.Overlay_OperatingMode,
                Describe(Recent(snapshot.Mode, MarstekMethods.EnergySystemGetMode), mode => mode.Mode, mode => mode.DisplayName)),
        ];
    }

    // The time left matters only while the battery carries the load; on mains, or before the source is known, the
    // row stays off the panel. The compact line and the tray tooltip still tell what would last or when it fills up.
    private static OverlayRow[] DescribeForecast(
        PowerSupply supply,
        RuntimeForecast forecast,
        ForecastSettings forecastSettings,
        DateTimeOffset now) =>
        supply.Source == PowerSource.Battery
            ?
            [
                new OverlayRow(
                    OverlayRowKind.Forecast,
                    Label: null,
                    ForecastPresenter.Describe(forecast, forecastSettings, now),
                    IsProminent: true,
                    ForecastPresenter.DescribeHint(forecast, forecastSettings)),
            ]
            : [];

    private static string DescribeChart(HistoryChart chart) => Phrases.Lines(
        SummarizeLoad(chart),
        chart.Summary is null
            ? null
            : Phrases.Format(
                Strings.Chart_Legend,
                Units.Format(chart.LoadCeiling),
                Units.Percent(chart.ChargeScale.FloorPercent),
                Units.Percent(chart.ChargeScale.CeilingPercent)));

    private static (string Text, Tone Tone, string? Hint) DescribeSupply(PowerSupply supply, EnergySystemStatus status, DateTimeOffset now)
    {
        switch (supply)
        {
            case { Source: PowerSource.Battery, Since: { } since }:
                var duration = Phrases.Duration(now - since, isExact: supply.IsSinceObserved);
                return (Phrases.Format(Strings.Overlay_OnBattery, duration), Tone.Warning, DescribeSince(Strings.Supply_BatterySince, supply, since, now));

            case { Source: PowerSource.Mains, Since: { } since }:
                // A top-up that keeps a full battery full comes and goes within a minute and is not worth a word.
                var isCharging = PowerSourceRule.IsChargingFromMains(status, supply.Source);
                var text = isCharging ? Strings.Overlay_OnMainsCharging : Strings.Overlay_OnMains;
                return (text, Tone.Positive, DescribeSince(Strings.Supply_MainsSince, supply, since, now));

            default:
                var (flowText, flowTone) = DescribeFlow(PowerSourceRule.BatteryPower(status, supply.Source)?.Flow);
                return (flowText, flowTone, null);
        }
    }

    private static string DescribeSince(string template, PowerSupply supply, DateTimeOffset since, DateTimeOffset now)
    {
        var text = Phrases.Format(template, Phrases.Moment(since, now));
        return supply.IsSinceObserved ? text : Phrases.Sentences(text, Strings.Supply_SwitchUnseen);
    }

    private static (string Text, Tone Tone) DescribeFlow(BatteryFlow? flow) => flow switch
    {
        BatteryFlow.Charging => (Strings.Overlay_Charging, Tone.Positive),
        BatteryFlow.Discharging => (Strings.Overlay_Discharging, Tone.Info),
        BatteryFlow.Idle => (Strings.Overlay_Idle, Tone.Secondary),
        _ => (Strings.Overlay_NoData, Tone.Secondary),
    };

    private static string DescribePower(Power? power) =>
        power is { } value ? Units.Format(value) : Strings.Overlay_ValueMissing;

    private static string Describe<TReading, TValue>(
        Reading<TReading>? reading,
        Func<TReading, TValue?> select,
        Func<TValue, string> format)
        where TValue : struct
    {
        if (reading is null)
            return Strings.Overlay_ValueMissing;

        return select(reading.Value) is { } value ? format(value) : Strings.Overlay_ValueUnavailable;
    }

    private static string DescribeBatteryPower(EnergySystemStatus? status, PowerSupply supply)
    {
        if (status is null)
            return Strings.Overlay_ValueMissing;

        return PowerSourceRule.BatteryPower(status, supply.Source) is { } battery
            ? Units.Format(battery.Power)
            : Strings.Overlay_ValueUnavailable;
    }
}

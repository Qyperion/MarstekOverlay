# Forecast and alerts

Both live in `src/Monitoring` and run inside `StationPipeline` on every new answer ([Architecture.md](Architecture.md)).

## Runtime forecast

`RuntimeForecaster.Update(snapshot, source, settings)` returns `RuntimeForecast(Status, EndsAt, StoredEnergy, StandbyRuntime)`.

| `ForecastStatus` | Meaning |
| --- | --- |
| `TimeToReserve` | discharging: `EndsAt` is when the charge reaches the reserve level |
| `TimeToFull` | charging: `EndsAt` is when the battery is full |
| `Standby` | on mains with an idle battery: `StandbyRuntime` is how long it would last at the present BACKUP load |
| `Collecting` | a steady flow has just begun; not enough samples yet |
| `LoadTooLow` | nothing to forecast: an idle battery while the source is not mains, or on mains a BACKUP load of 20 W or less |
| `ReserveReached` | not charging and at or below the reserve level |
| `InsufficientData` | the station has not reported charge, capacity or power |

**Averaging.** The power is averaged over the last 5 minutes (`AveragingWindow`, kept by `PowerAverage`), one sample per answer at least 10 s apart. The first estimate needs three samples spanning 55 s — one minute at the default polling. A change of direction, or a status with nothing to forecast, starts the averaging over. A snapshot that repeats the previous readings, as after a failed poll, repeats the previous forecast instead of counting as a sample. With changed settings the same readings are estimated anew at once, still without a sample of their own or a second step of the stored energy.

**The sums**, with power measured at the station's outputs:

- to reserve: `(stored − reserve) × discharge efficiency ÷ average discharge`
- to full: `(capacity − stored) ÷ charge efficiency ÷ average charge`
- standby: as "to reserve", at the average BACKUP load, which must exceed 20 W — the least a battery has to give to count as discharging (`BatteryPowerReading`)

Which efficiency applies in which direction is decided in one place, `ForecastSettings.InTheBattery` and its reverse `AtTheOutputs`.

The two efficiencies are user settings (`ForecastSettings`): `DischargeEfficiencyPercent` 90 — an allowance, the station measured about 92 — and `ChargeEfficiencyPercent` 97, which fits the measured charging power but has not yet been checked over a long charge ([StationBehaviour.md](StationBehaviour.md)). Charging is not checked against the reserve: at 5% and charging the forecast is the time to 100%. Top-ups of a full battery (`PowerSourceRule.IsTopUp`) count as neither charging nor an interruption.

The forecast stores the **moment** something ends, not a duration. The presenter shows `EndsAt − now`, so the text keeps moving between polls. Durations and clock times are rounded to 10 minutes; a time that is not today is named "tomorrow" or by its weekday.

The time to full is linear. A station that slows down near 100% would make it optimistic; the measured one did not slow down.

### Stored energy

`StoredEnergyEstimator` combines two readings that are each half of what is needed:

- `bat_capacity` from `Bat.GetStatus` is precise (0.1% steps) but arrives every 5 minutes;
- `bat_soc` from `ES.GetStatus` arrives every 30 s but in whole percents — 51 Wh steps on a 5.12 kWh battery.

The latest `bat_capacity` is the base. On each `ES.GetStatus` answer it is moved by battery power × elapsed time — divided by the discharge efficiency while discharging, multiplied by the charge efficiency while charging — and kept within ±1% of the present `bat_soc`. A new `Bat.GetStatus` answer replaces what has accumulated. Without `Bat.GetStatus` the estimate is `bat_soc × bat_cap`.

The band is ±1% rather than the exact rounding rule because rounding may differ between firmwares; it also guards against a `bat_capacity` in other units. The panel row "Energy left" shows this estimate, not the raw `bat_capacity`.

## Alerts

`AlertMonitor.Update(...)` returns `AlertReport(Active, Raised)`: `Active` is what is wrong now — the warning rows of the panel; `Raised` is what has just happened — a Windows notification.

| `AlertKind` | Condition | Defaults |
| --- | --- | --- |
| `MainsLost` | the source switched to battery, or the app started during an outage | — |
| `MainsRestored` | the source switched back; carries the finished `Outage` | — |
| `LowCharge` | the charge reached a threshold; the lowest threshold is critical | 50, 30, 15% |
| `BatteryTemperature` | outside the limits | 5–45 °C |
| `FlowForbidden` | `charg_flag` or `dischrg_flag` is false; forbidden discharge is critical | — |
| `StaleData` | no answer for longer than the limit | 5 min |

Rules, each the result of a wrong notification that would otherwise happen:

- **Once, with a margin.** Every condition has a latch: it notifies when the condition sets in and re-arms only when the condition has gone with room to spare — the charge 2% above the threshold, the temperature 1 °C inside the limits. Several thresholds crossed between two polls give one notification, about the lowest.
- **Not about the old.** The first evaluation of a condition — after a start, after its kind is switched on, for a threshold just added — does not notify if the condition already holds. Critical conditions are the exception.
- **Mains: only a switch that was seen.** `MainsLost` and `MainsRestored` come from `PowerSourceTracker.SourceChanged` with `IsWatched`: the tracker was following a known source when it changed. Whether the moment could be dated does not matter — an outage that begins on a full battery, the most common kind, is dated late (`IsSinceObserved` false) and still notifies. Mains found after a start, and a source determined after a gap of more than 10 minutes, do not notify. A top-up does not change the source.
- **Found during an outage.** A source determined while it was unknown — the first in this run, or the first after a gap of more than 10 minutes (the computer slept, the station fell silent) — if it is the battery, raises `MainsLostAlert(IsSwitchSeen: false)` with a text of its own: the outage began while the app was not following the station. This covers an outage resumed from the journal, and one still going on after the gap, which is told of again. Mains found after the gap and then lost give the ordinary `MainsLost`.
- **Critical charge while charging.** A critical charge found at first sight while `PowerSourceRule.IsChargingFromMains` is not news: it is latched without a notification, and the next fall below the lowest threshold, after the charge has risen past the margin, notifies as usual.
- **The panel shows the raw condition, the notification the latched one.** At 51% after a 50% threshold the warning is gone from the panel, while a new notification is possible only after 52%.
- **Low charge while charging.** `PowerSourceRule.IsChargingFromMains` keeps `LowChargeAlert` out of `Active`: the charge is already rising. The latch and `Raised` are untouched, and the colour of the charge bar still follows the thresholds.
- **An old battery answer is no answer.** Temperature and flags are taken only from a `Bat.GetStatus` answer younger than `ReadingFreshness.TrustedFor` — three slow polls, each up to one fast poll cycle late, 16.7 minutes by default. The same function marks stale cards in the Details window.
- **`StaleData` waits at least two polling intervals,** whatever the setting says: an answer up to one interval and a poll's few seconds old is the station answering on schedule. At the default 30 s this changes nothing. The rule is `Alerts/SilenceLimit`, which the settings window reads too: when the limit in effect is longer than the one typed, the field says how long it is.
- **`StaleData` counts only the time the app itself was waiting.** After the computer sleeps, or the station address changes, the count starts over; otherwise the first failed poll after waking would report eight hours of silence.
- **A switched-off kind is not evaluated at all** — no notification, no panel warning, and its latch is forgotten.

Thresholds come from `AlertSettings.UsableChargeThresholds()` (1–100, no repeats, ascending — the file may have been edited by hand). `AlertSettings.ChargeLevelAt(soc)` gives the level behind the colours: comfortable above the highest threshold, critical from the lowest, low in between.

### Delivery

`StationPipeline.AlertRaised` → `AlertNotifier` (`src/Desktop/Services`) → `AlertPresenter` for the text → `INotificationChannel`. The channel is the tray icon: `NotifyIcon.ShowBalloonTip`, which Windows 10 and 11 show as an ordinary notification and keep in the notification centre, with nothing to register. It follows that notifications depend on the icon: if Windows hides it or the app's notifications are switched off, they do not arrive, and the notification centre names the app after the path of its exe.

Each raised alert is also logged in English as `Alert raised: …`.

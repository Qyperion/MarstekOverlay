# Power source and the outage journal

The station does not say whether the mains are present, so the app concludes it from `ES.GetStatus`. The reasoning rests on how a Venus E 3.0 behaves ([StationBehaviour.md](StationBehaviour.md)): while the battery is not full the station charges it whenever the mains are there, and on a full battery it passes the mains to the BACKUP output invisibly.

All of it is an assumption about one kind of setup, which is why it lives in one place: `PowerSourceRule` in `src/Monitoring/Supply`.

## Evidence in one answer

`PowerSourceRule.Read(status, previous)` classifies an answer; `previous` is the `GridEnergyCounters` of the answer before it:

| `SupplyEvidence` | When | Means |
| --- | --- | --- |
| `GridAtWork` | `ongrid_power` differs from zero by more than 10 W, or `total_grid_input_energy` / `total_grid_output_energy` grew since the previous answer | direct proof of mains |
| `DrainingBattery` | the grid port is silent, the charge is below 100%, the battery discharges faster than 20 W | with mains the station would be charging — so there are none |
| `FullBattery` | the grid port is silent, the charge is 100% | either a charged station on mains or the first minutes of an outage |
| `None` | anything else | nothing to conclude |

## From evidence to a source

`PowerSourceTracker.Update(snapshot)` returns `PowerSupply(Source, Since, IsSinceObserved)`:

- `GridAtWork` → **mains**, at once.
- `DrainingBattery` twice in a row → **battery**, counted from the first of the two answers. One such answer changes nothing: it is also what the instant before a top-up looks like. This is why an outage is announced one poll (30 s) late. The delay is accepted: no out-of-turn poll is sent to confirm sooner.
- Right after `FullBattery` (the latest answer that told anything of the grid, `GridAtWork` or `FullBattery`, was a full battery) the run of `DrainingBattery` must be three answers long and last `PowerSourceRule.LongestTopUpDelay` (2 minutes) from its first answer. A station that settled at 99% may wait before its top-up — seen up to a minute — and looks like an outage meanwhile. Such an outage is still counted from the first answer, but announced at least two minutes late.
- `FullBattery` leaves a known source as it is, and makes an unknown one mains. It cannot end an outage.
- A gap of more than 10 minutes between answers (`LongestUnwatchedGap`) resets the source to unknown.
- `IsSinceObserved` is true only for a switch between two known sources that the app could date: not for an outage that follows a full battery (the mains went unseen to the BACKUP output, so it began earlier), and not for a switch first seen in the answer after a restart. Otherwise durations are shown as "at least".

`SourceChanged(PowerSourceChange(Previous, Current, IsWatched))` fires only when the source itself changes, including unknown → mains after a start. `IsWatched` is true when the tracker was following a known source at the change — a switch it saw happen, however late it could date it; the mains lost and restored notifications go out only for those.

**Consequences worth knowing**

- An outage that starts on a full battery becomes visible when the charge drops to 99%, and is dated to that moment with `IsStartObserved` false: the journal, the panel and the "mains back" notification say "at least". Its duration and energy are understated by those minutes (up to the time between top-ups).
- The rule expects a station that charges to 100% whenever it can. With a charging schedule or a charge limit, "mains without charging" would look like an outage.
- If the station ever delays a top-up for longer than `LongestTopUpDelay` after dropping to 99%, a false outage results.

## Battery power

`PowerSourceRule.BatteryPower(status, source)` is the battery power to trust. On mains with a silent grid port the load does not come from the battery, so the calculated power is `pv − ongrid`, that is 0 — not the raw balance, which would be minus the load. The panel, the forecast, the stored samples and the Details window use this value.

`BatteryFlow` (charging / discharging / idle) has one dead zone for everyone: above +10 W is charging, below −20 W is discharging.

Related rules in the same class: `IsTopUp` (charging at 99% or more is a top-up, not charging), `IsChargingFromMains` (hides the low-charge warning), `IsBatteryPowerInDoubt` (the raw balance cannot be trusted while the source is unknown — the Details window and the **Test connection** report, which has a single answer and never knows the source, say so next to the value; the panel's Battery row shows the number alone).

## Outage journal

`OutageJournal` (`src/Monitoring/Outages`) follows the tracker: `Record(supply, snapshot)` after every `tracker.Update`.

An `Outage` holds `StartedAt`, `EndedAt`, `IsStartObserved`, `IsEndObserved`, `ConsumedEnergy`, `StartSocPercent`, `EndSocPercent`, and `LatestGridEnergy` — the grid energy counters of the latest answer it was seen in.

- **Energy** is the BACKUP output power integrated over time by trapezoids between answers. It misses the last stretch before the first answer that shows mains (up to 30 s). While no outage goes on, the journal keeps the answers of the latest `LongestUnwatchedGap`, so an outage confirmed several answers after its first is counted, and its starting charge taken, from that first answer.
- **The open record is saved on every poll,** so a crash loses one poll at most.
- **Restart.** The journal's constructor picks up an open outage and hands it to the tracker (`Resume`), together with the moment it was last seen and its `LatestGridEnergy`. A gap up to `LongestUnwatchedGap` (10 minutes) then counts as watched, as between two answers of one run, except that nothing that changed meanwhile was seen:
  - the first answer still on battery continues the outage from the same start;
  - the first answer with the grid at work — the grid port busy, or its energy counters grown since the stored ones, as they do when the mains came back and charged the battery meanwhile — closes it where it was last seen, with `IsEndObserved` false, and `MainsRestored` goes out;
  - after a longer gap the source is unknown, and the outage is closed at the moment it was last seen, with `IsEndObserved` false.
  An outage stored before the counters were kept has none: a restart during which the mains came back and charged the battery to full then keeps it open until the first top-up.
- **End** is the first answer that shows mains. Without a restart the same holds after a break in the answers: up to 10 minutes the end is observed, beyond that it is the last moment seen.
- Only an open outage is restored after a restart. A period on mains starts over.

Reading: `Ongoing`, `LatestFinished`, `Since(from)`, `Between(from, to)`, `FirstStartedAt`. `OutageTotals.Over(outages, from, to, load)` gives the count, the time on battery and the energy for a period; an outage that crosses a boundary counts by the part inside (`OutagePart.Within`). The journal keeps one energy figure per outage, so the part's energy is a share of it. `RecordedLoad` works the share out from the backup output in the samples: between two samples no further apart than `SampleHistory.LongestBridgedGap` the output is taken to change evenly, as in the journal; whatever energy the samples do not account for is spread evenly over the time they do not cover. Without samples this is the share of the time. `SampleHistory.LoadAcross(outages, bounds)` reads samples only for the outages that cross one of the bounds, and only as far as they reach. Samples are kept for `RetentionDays` (7 by default), so once they are gone a past boundary falls back to the share of the time and its totals may shift a little. A part that a bound cuts off is exact at the cut even when the outage's own start or end was not seen. Calendar boundaries come from `CalendarPeriods` (`DayStart`, `WeekStart`, `MonthStart`) and `CalendarMonth`.

## Storage

`src/Storage` keeps the journal in LiteDB, in `history.db` next to the user settings:

- `HistoryDatabase` owns the one file, its `BsonMapper`, and hands out collections through `Collection<T>(name)`. If the file cannot be opened — another program holds it; a second copy of the app on the same data folder exits before it gets here ([DesktopUi.md](DesktopUi.md)) — the database lives in memory for the session and the reason goes to the log. Every later call on the file, the stores' own included, goes through `Try(operation, fallback, report)`: a failure of the file — LiteDB's own errors, I/O and access errors, and the `ObjectDisposedException` a closed database answers a query with — is logged and answered with the fallback (nothing found, nothing deleted, nothing saved) instead of thrown.
- `LiteDbOutageStore : IOutageStore` — collection `outages`, document `OutageDocument`, dates in UTC to the millisecond (`AsStored`); `Save` returns the outage with its moments cut the same way, so the journal holds what it would read back after a restart. A failed read or write is logged and does not bring the panel down.
- The journal is kept indefinitely; nothing deletes from it by age. The user can delete from it in the settings window ([HistoryAndCharts.md](HistoryAndCharts.md), "Deleting on request"): `DeleteFinishedBefore(moment)` removes the outages that ended before a moment and never the ongoing one, which is the state of the station rather than its history.

A new kind of record is an interface in `src/Monitoring`, a document class and a store in `src/Storage`, and a registration in `AddStorage`.

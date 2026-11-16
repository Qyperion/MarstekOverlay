# Architecture

Marstek Overlay is a Windows desktop app (WPF, .NET 11) that reads a Marstek station over its local Open API and shows what it reports. It never writes to the station.

## Projects

```
src/LocalApi        the Open API client: UDP JSON-RPC transport, typed models, firmware profiles
src/Monitoring      polling, the snapshot, and everything derived from it — no UI, no storage engine
src/Storage         LiteDB behind the store interfaces Monitoring declares (file history.db)
src/Desktop         the WPF app: panel, tray icon, settings and Details windows, localization
tests/UnitTests     LocalApi, Monitoring, Storage, StationSimulator  (net11.0)
tests/DesktopTests  the presentation logic, view models and shell services of Desktop, both languages (net11.0-windows)
tests/TestSupport   what both test projects build on: stores in memory, station readings, a poll clock, the pipeline steps
tools/StationSimulator  a Venus E 3.0 on 127.0.0.1 for checking the running app
tools/Release, tools/Installer  the files of a release and their installer
```

Building, the installer and releases: [BuildAndRelease.md](BuildAndRelease.md).

The view-model tests run over the real pipeline with stores in memory (`DesktopTests/TestSupport/StationHarness`). A view model that starts a timer is tested on a thread of its own with a dispatcher (`TestSupport/UiThread`); the dispatcher is not pumped, so a timer never ticks, and what a tick does is reached through the method it calls. The texts the Desktop tests expect are checked against culture data of their own (`TestSupport/Cultures.resx`), so an update of Windows or ICU cannot change them. The shell services are tested without windows: `SettingsFollower` over the harness with a client that notes which station it is asked, `TrayMenu` without its `NotifyIcon`, and `Autostart` on a key of its own under `HKCU\Software\MarstekOverlay.Tests`, laid out as Windows lays out `Run` and `StartupApproved\Run` and erased afterwards.

References go one way: `LocalApi` ← `Monitoring` ← `Storage` ← `Desktop`. `Monitoring` declares `IOutageStore` and `ISampleStore`; `Storage` implements them, so `Monitoring` knows nothing about LiteDB, and neither of them knows about WPF.

The app follows one station. The journal and the samples are not tied to it: a changed address carries on the same history. Telling stations apart by MAC or following several at once is not a goal.

## From a datagram to the screen

1. **`StationMonitor`** runs the polling loop on a thread-pool thread. Each poll is a `StationReader.ReadAsync` call that sends the requests one by one and returns a `StationSnapshot` — the latest answer to every request, each with the time it arrived. A request that fails keeps its previous reading, so one silent method does not blank the rest. The monitor raises `SnapshotChanged` after every answer, not only at the end of a poll, because a full poll takes about eight seconds.
2. **`StationPipeline`** is the only consumer of snapshots. It moves each one to the UI thread, whose synchronization context the app hands it at start-up, and runs the steps in a fixed order, each relying on the one before:
   `PowerSourceTracker.Update` → `OutageJournal.Record` → `SampleHistory.Record` → `AlertMonitor.Update` → `RuntimeForecaster.Update`.
   The result is a `StationState` (`Latest`, event `StateChanged`); alerts that have just been raised go out through `AlertRaised` first.
3. **Readers.** `OverlayViewModel` (panel and tray icon), `DetailsViewModel` (Details window) and `AlertNotifier` (Windows notifications) only read the pipeline. None of them needs another to exist.

The tracker, the journal and the history are not thread-safe. They are touched only on the UI thread — by the pipeline when it writes and by the view models when they read — which is why the pipeline marshals instead of locking. The price is that `history.db` is written from the UI thread.

Records are deleted on the user's request through the pipeline as well (`DeleteBefore`, event `RecordsDeleted`), so the state and its readers never name a record that is gone ([HistoryAndCharts.md](HistoryAndCharts.md)).

A change of user settings re-runs the latest snapshot at once, so a switched-off alert kind disappears from the panel without waiting for the next poll. The same snapshot is never counted twice: the tracker, the journal, the history and the forecaster each recognise a repeated reading by its arrival time.

## Presentation

The view models hold no texts of their own. Presenters turn a `StationState` into finished, localized state:

| Presenter | Produces |
| --- | --- |
| `OverlayPresenter` | `OverlayState`: header, charge, rows, alerts, chart, compact line, tray summary |
| `ForecastPresenter` | the forecast: its row with the hint, the brief one of the compact line |
| `OutagePhrases` | an outage alike on the panel, in the journal and in the notifications: duration, charge, what was not seen |
| `AlertPresenter` | notification titles and texts, the warning rows of the panel |
| `DetailsPresenter` | outage totals, journal rows, the line above the charts, the age of a card, the word on an export |
| `StoredDataPresenter` | the word on a deletion of stored data |
| `StationSections` | every value of a snapshot as labelled sections — the cards of the Details window and the text report of **Test connection** |
| `ErrorPresenter` | a `PollError` in the interface language |
| `Phrases`, `Units` | durations, moments, values with their units; `Phrases.Format` is the one way a text of `Strings` is filled in |

More on the windows themselves: [DesktopUi.md](DesktopUi.md).

## Settings

Two kinds, kept apart on purpose:

- `appsettings.json` ships with the app and is bound to options classes at start-up (`MarstekApiSettings`, `PollingSettings`, `HistorySettings`, `LoggerOptions`, `StorageSettings`), each with a validator that fails the start.
- `settings.json` holds what the user sets in the app (`UserSettings`). `UserSettingsStore` loads it, puts right what a hand edit broke (`UserSettingsRepair`), applies changes through `Change(...)` and raises `Changed`; everything that depends on a setting follows that event, which is what makes the settings window apply live.

Keys and file locations: [Configuration.md](Configuration.md).

## Related documents

- [MarstekLocalApi.md](MarstekLocalApi.md) — the protocol as the client implements it
- [StationBehaviour.md](StationBehaviour.md) — what a real Venus E 3.0 was measured to do
- [PowerSourceAndOutages.md](PowerSourceAndOutages.md) — telling mains from battery, the outage journal
- [ForecastAndAlerts.md](ForecastAndAlerts.md) — runtime forecast, notifications
- [HistoryAndCharts.md](HistoryAndCharts.md) — stored samples and the numbers behind the charts

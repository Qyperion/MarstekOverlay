# Configuration

Two files with different owners: `appsettings.json` ships with the app, `settings.json` is written by the app from the settings window.

## Files

| File | Where | What |
| --- | --- | --- |
| `appsettings.json` | next to the exe | protocol timings, polling, retention, logging, storage location |
| `settings.json` | `%APPDATA%\MarstekOverlay\` | everything set in the settings window |
| `history.db` | `%APPDATA%\MarstekOverlay\` | the outage journal and the samples (LiteDB); may be deleted while the app is not running |
| `diagnostics.log` | `%APPDATA%\MarstekOverlay\` | the log, always in English; rolls at 5 MB into `diagnostics_001.log` and so on, three files kept |

What `history.db` and the log hold can be deleted from the settings window, whole or except for the latest day, week or month ([HistoryAndCharts.md](HistoryAndCharts.md)).

The data folder and the file names come from the `Storage` section. Any setting of `appsettings.json` can be overridden on the command line, which is how the app is run against a scratch folder:

```
MarstekOverlay.exe --Storage:RootDirectory=C:\Temp\overlay --LogConfiguration:FileLogMinimumLevel=Debug
```

A portable exe of a release is one file and comes without `appsettings.json`: it runs on the defaults listed below, which are those of the code. To change a setting, put a file with just that key next to the exe:

```json
{ "Polling": { "DisabledMethods": [ "Bat.GetStatus" ] } }
```

"Next to the exe" is `Settings/ExeFolder`, the folder of the process's exe. It is not `AppContext.BaseDirectory`: the portable exe with the .NET runtime inside unpacks itself into `%TEMP%\.net` and has its base directory there ([BuildAndRelease.md](BuildAndRelease.md)).

The installer of a release leaves an `appsettings.json` that is already in the install folder as it is, so what a user changed there survives an update; a key such a file lacks takes the default of the code, which is the same as in the shipped file. `tools/Publish/Publish-App.ps1` replaces the file ([BuildAndRelease.md](BuildAndRelease.md)).

A relative `RootDirectory` puts the data inside the install folder, which `tools/Publish/Publish-App.ps1` mirrors from `publish`. The mirror therefore neither copies nor deletes the files that `Storage` and `LogConfiguration` of the published `appsettings.json` name (an environment variable `Section__Key` wins, as in the app; the default names are always spared), nor the top folder below the install folder that holds them. It refuses to mirror when it cannot tell data from the app: `appsettings.json` does not parse, the data folder is a folder of the build, or a shipped file matches a data name and would never be updated.

## appsettings.json

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| `MarstekApi` | `RequestTimeoutMs` | 3000 | wait for one answer |
| | `RetryCount` | 1 | repeats of an unanswered request |
| | `MinDelayBetweenRequestsMs` | 1000 | pause between requests; a station drops requests sent closer than about 0.5 s. A poll lasts a pause per request, so it also lengthens how long a reading counts as fresh (`ReadingFreshness`) |
| | `DiscoveryDurationMs` | 3000 | how long a search for stations listens |
| `Polling` | `IntervalSeconds` | 30 | the fast poll (1–120); the forecast window, the outage rule and the silence alert are minutes long and need several polls inside them |
| | `SlowIntervalSeconds` | 300 | identity, battery details, Wi-Fi, Bluetooth (from `IntervalSeconds` to 3600) |
| | `DisabledMethods` | `[]` | wire names of methods never sent, for example `"Bat.GetStatus"` |
| `History` | `RetentionDays` | 7 | how long samples are kept (1–3650); about 2900 samples a day at the default polling |
| `LogConfiguration` | `IsEnabledFileLog` | true | |
| | `FileLogMinimumLevel` | `Information` | `Debug` adds every datagram sent and received |
| | `LogFilePath` | `diagnostics.log` | relative to the data folder |
| `Storage` | `RootDirectory` | empty | the data folder; empty means `%APPDATA%\MarstekOverlay`, a relative path is taken from the folder of `MarstekOverlay.exe` |
| | `UserSettingsFileName` | `settings.json` | relative to the data folder; must name a file |
| | `DatabaseFileName` | `history.db` | relative to the data folder; must name a file other than the settings |

Invalid values fail the start with a message box naming the section.

## settings.json

| Path | Meaning |
| --- | --- |
| `Language` | `en` or `uk`; empty, or a code the app does not speak, means English |
| `Station.Host`, `Station.Port` | the station's IPv4 address, four decimal numbers without leading zeros (a short form such as `192.168.1` counts as no address: nothing is polled, the log says so at start and the settings window marks the field as it opens), and UDP port |
| `Overlay.Left`, `Overlay.Top` | where the panel was left |
| `Overlay.IsTopmost`, `OpacityPercent`, `ScalePercent`, `IsCompact`, `IsClickThrough` | look and behaviour of the panel |
| `Overlay.IsHiddenAtStartup` | the app starts without showing the panel; `false` when absent |
| `Overlay.HiddenRows` | default-on rows that are switched off |
| `Overlay.ShownRows` | default-off rows that are switched on |
| `Overlay.IsChartShown`, `Overlay.ChartHours` | the panel chart and its period (1, 3, 12, 24; any other number becomes the longest of them not above it, or 1) |
| `Forecast.ReserveSocPercent` | the charge the forecast counts down to (12) |
| `Forecast.DischargeEfficiencyPercent`, `ChargeEfficiencyPercent` | losses in each direction (90, 97) |
| `Alerts.DisabledKinds` | alert kinds that are switched off |
| `Alerts.ChargeThresholdsPercent` | 50, 30, 15 |
| `Alerts.MinBatteryTemperatureCelsius`, `MaxBatteryTemperatureCelsius` | 5, 45 |
| `Alerts.StaleAfterMinutes` | 5 |
| `Details.ChartHours` | the period of the Details charts |
| `Details.Window` | `Left`, `Top`, `Width`, `Height`, `IsMaximized`; `null` until the window has been closed once |

Rows are named by `OverlayRowKind`: `Forecast`, `Battery`, `StoredEnergy`, `CurrentOutage`, `LatestOutage`, `Grid`, `BackupOutput`, `Solar`, `BatteryTemperature`, `WifiSignal`, `OperatingMode`. The first two, `Grid`, `BackupOutput` and `Solar` are on by default.

**Autostart is not in this file.** It exists only as a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, which Task Manager can disable through a value of the same name under `…\Explorer\StartupApproved\Run` ([DesktopUi.md](DesktopUi.md)).

### Older files

A file saved by an older version is read as it is. `UserSettingsStore.Upgrade` moves `Forecast.UsableEnergyPercent` to `DischargeEfficiencyPercent`; keys that no longer exist (`IsUpdateTimeRelative`) are skipped; new names appear in the file with the first save. A name in `HiddenRows`, `ShownRows` or `DisabledKinds` that this version does not know is left out with a warning in the log; the rest of the file is read.

### Damaged files

The file is written to `settings.json.tmp` next to it and then put in its place, so a write cut short by a crash or a power loss leaves the previous file whole.

A file that cannot be read is logged and replaced by defaults in memory. A file held by another process is read three times, 200 ms apart, before that. Nothing is written over such a file until the user changes something; the first write then copies it to `settings.json.bak` (one copy, replaced each time) before saving.

Whatever a file edited by hand holds, the app starts: a section or a list that is `null` takes its defaults, a number outside what the settings window accepts is pulled to the nearest bound (`UserSettingsRepair`), a panel chart period the window does not offer becomes the one it marks (`OverlaySettings.OfferedChartHours`, which the panel draws by as well), a charge threshold outside 1–100 is left out, and battery temperature limits that leave no range fall back to 5 and 45. An address or a port that stands for no station is left as it is and only told of. Every correction is a warning in the log; the file keeps its values until the next save.

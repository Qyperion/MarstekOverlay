# Contributing

Reports are as welcome as code. The app has been checked on one model and one firmware, so a [station report](https://github.com/Qyperion/MarstekOverlay/issues/new?template=station_report.yml) from another one helps even when everything works, and a [bug report](https://github.com/Qyperion/MarstekOverlay/issues/new?template=bug_report.yml) with the diagnostic log usually says what went wrong.

## Building

Windows and the [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0); `global.json` pins it.

```
dotnet build MarstekOverlay.slnx
dotnet test --solution MarstekOverlay.slnx
dotnet format MarstekOverlay.slnx
dotnet run --project src/Desktop/Desktop.csproj
```

The build is expected to stay without warnings: it checks the style rules of `.editorconfig` as well. `dotnet format` applies them, the line endings and the encoding included, so run it before committing. The installer and the files of a release are described in [docs/BuildAndRelease.md](docs/BuildAndRelease.md).

## Trying a change without a station

`tools/StationSimulator` answers the read methods on `127.0.0.1` the way a Venus E 3.0 does, in the state you ask for:

```
dotnet run --project tools/StationSimulator -- --scenario battery --port 30001
dotnet run --project src/Desktop/Desktop.csproj -- --Storage:RootDirectory=C:\Temp\overlay --Polling:IntervalSeconds=5 --Polling:SlowIntervalSeconds=30
```

The second command keeps the app's settings and history in a folder of its own, apart from the copy you use every day, and polls faster than usual. In its settings window give the address `127.0.0.1` and the simulator's port. `--help` lists what the simulator can do: charging, a top-up, a method that goes silent or is refused, an outage that starts or ends at a given second.

## What a change should keep

- **The app only reads.** Methods that change how a station works (`ES.SetMode`, `DOD.SET`, `Ble.Adv`, `Led.Ctrl` and the like) are left out on purpose and will not be merged.
- **A poll stays short.** A station drops requests sent closer than about half a second, so the app sends one a second, and every extra request lengthens every poll. `Bat.GetStatus` stays in the slow cycle: some firmware restarts on it.
- **Layers.** Logic without a user interface belongs in `src/Monitoring` (the protocol in `src/LocalApi`) with tests in `tests/UnitTests`; `src/Desktop` only presents, and the texts it composes are tested in `tests/DesktopTests`. The overview is [docs/Architecture.md](docs/Architecture.md).
- **Units by firmware.** A value whose unit on the wire depends on the model or the firmware goes through `LocalApi/Firmware/FirmwareProfile`; an unknown model scales nothing. Support for another model starts from what that station really answers — a station report, or a log at the `Debug` level.
- **Languages.** Code, comments and log messages are English. Every text the user sees is a key in `src/Desktop/Localization/Strings.resx` and the same key in `Strings.uk.resx`. A test that runs in both languages words what it expects in English through `Translated("...")`, with the Ukrainian text in `tests/DesktopTests/TestSupport/Translations.uk.resx`.
- **Tests.** New logic comes with a test, laid out as `Arrange` / `Act` / `Assert`, with FluentAssertions 7. Version 8 is under a commercial licence, so it is not upgraded.
- **Comments** say what the code cannot: why, a hidden invariant, a measured quirk of the station.
- **Documentation.** When behaviour changes, `README.md` and `README.uk.md` change together, and so does the matching file in `docs/` when the mechanics did.

## Pull requests

For anything larger than a fix, open an issue first, so that the direction is agreed before the work is done. One topic per pull request; the `CI` workflow builds it and runs the tests.

Contributions are accepted under the [MIT licence](LICENSE) of the project.

# Build and release

## Building from the sources

The [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0) on Windows is all it takes; `global.json` pins it.

```
dotnet build MarstekOverlay.slnx
dotnet test --solution MarstekOverlay.slnx
dotnet run --project src/Desktop/Desktop.csproj
```

The app in a folder of its own, for a machine with the .NET 11 Desktop Runtime:

```
dotnet publish src/Desktop/Desktop.csproj -c Release -r win-x64 --self-contained false -o publish
```

## A developer's own install

`tools/Publish/Publish-App.ps1` builds the app from the current sources, installs it into `C:\Program Files\MarstekOverlay` and adds it to the startup:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Publish-App.ps1
```

It is meant for whoever works on the code; everyone else is better served by the installer of a release.

- The script builds the `publish` folder, stops the installed copy if it is running, replaces its files (Windows asks for administrator rights for that), writes the startup entry of the current user, puts **Marstek Overlay** shortcuts on the desktop and in the Start menu, and starts the installed copy.
- The install folder becomes an exact copy of `publish`, so changes made to its `appsettings.json` are lost: make them in `src/Desktop/appsettings.json`.
- The data in `%APPDATA%\MarstekOverlay` is not touched. If `Storage:RootDirectory` points into the install folder, neither the files that the `Storage` and `LogConfiguration` sections of `appsettings.json` name (with their backups and the rolled-over logs) nor the subfolder that holds them are erased there; files with the default names `settings.json`, `history.db` and `diagnostics.log` are always spared.
- If the installed copy is not running a few seconds after its start — most often because another copy already works on the same data, and the new one only brought up its panel and exited — the script warns and names the path and the version of every copy that is running. Close that copy from its tray menu and start the installed one.
- An elevated console will do when it belongs to the account signed in to Windows: the startup entry and the shortcuts go to that account all the same, and the app is started through its shell. A console running as another account is refused — the entry and the shortcuts would go to that account — unless the script is asked only to build `publish` or to leave the startup entry, the shortcuts and the start alone.
- If the files could not be copied — say, the request for rights was answered "No" — the script starts the copy it had stopped again.

Switches: `-PublishOnly` builds `publish` and stops, `-NoAutostart` leaves the startup entry alone, `-NoShortcuts` creates no shortcuts, `-NoStart` does not start the app, `-InstallDirectory <path>` names another install folder.

`tools/Publish/Remove-App.ps1` takes that install away again with everything it left behind: the running copy, the install folder, the startup entry and the shortcuts that launch it, the data in `%APPDATA%\MarstekOverlay`, the firewall rules made for it, and what Windows remembers of its icon in the notification area and of its notifications.

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Remove-App.ps1
```

- It lists what it found and removes it only once that is confirmed; the data cannot be brought back. `-WhatIf` only lists, `-KeepData` leaves the settings, the history and the log.
- A startup entry or a shortcut that launches a copy in another folder — the one the installer of a release puts in — stays.
- The data is looked for where the installed `appsettings.json` puts it. The default folder is removed whole; a folder that `Storage:RootDirectory` names elsewhere loses only the files of the app.
- Once the install folder is gone the data stays: by then it may be that of another copy, which keeps its own in the same default folder.
- Another copy of the app that is running may hold the data, so the script asks for it to be closed first. Copies are told by the product name of the exe, since a portable exe runs under the name of its release file.
- Windows 11 keeps a record of a notification-area icon for every path the app was started from. The record of the installed copy goes, and so do those of copies that are no longer on the disk.

## The files of a release

`tools/Release/Build-Release.ps1` builds everything a release offers into `artifacts/release`:

| File | Made from |
| --- | --- |
| `MarstekOverlay-Setup-<version>.exe` | the installer, built by [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3 or later from a self-contained publish into a folder |
| `MarstekOverlay-<version>-win-x64-self-contained.exe` | `dotnet publish --self-contained true -p:PublishSingleFile=true`: one exe with the .NET runtime inside |
| `MarstekOverlay-<version>-win-x64.exe` | `dotnet publish --self-contained false -p:PublishSingleFile=true`: one exe that needs the .NET 11 Desktop Runtime |
| `SHA256SUMS.txt` | the sums of the three |

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Release/Build-Release.ps1
```

`-SkipInstaller` builds the portable exes alone, on a machine without Inno Setup. The version is `Version` in `src/Desktop/Desktop.csproj`; `-Version` only checks it, which is how a tag that names another version is refused. The symbols are embedded in the assemblies, so a failure in the log of a released build keeps its line numbers.

**A portable exe is the whole app in one file.** No `appsettings.json` comes with it: it runs on the defaults of the code, which are those of the shipped file, and reads a file put next to it ([Configuration.md](Configuration.md)). `LICENSE` and `THIRD-PARTY-NOTICES.md` are installed by the installer; for a portable exe they are in the repository and in the source archive GitHub attaches to every release.

**The exe with the runtime inside unpacks itself.** On .NET 11.0 a WPF app published as one self-contained file does not start: `DirectWriteForwarder.dll` and `System.Printing.dll` hold native code and cannot be loaded from inside the exe ([dotnet/wpf#11842](https://github.com/dotnet/wpf/issues/11842)). Until that is fixed the exe is published with `IncludeAllContentForSelfExtract`: on its first start it unpacks everything, about 145 MB, into `%TEMP%\.net\<name of the exe>\` and runs from there, and unpacks again if that folder is cleared. `EnableCompressionInSingleFile` keeps the download at 70 MB instead of 160. The SDK calls this a legacy mode and warns about it (`NETSDK1244`), which the script turns into a message. Once the fix has shipped, drop both and publish with `IncludeNativeLibrariesForSelfExtract` instead.

In that mode `AppContext.BaseDirectory` is the folder the exe unpacked into, not the one it lies in. Whatever is meant to be next to the exe — `appsettings.json`, a relative `Storage:RootDirectory` — therefore goes by `Settings/ExeFolder`, the folder of the process's exe.

**The installed copy is a folder, not one exe,** so it starts without unpacking anything. Only `en` and `uk` satellite resources of the framework are published (`SatelliteResourceLanguages` in `Desktop.csproj`): the app speaks no other language, and without them that folder holds a dozen folders less.

## The installer

`tools/Installer/MarstekOverlay.iss`, in English and Ukrainian.

- **For the current user, without administrator rights.** It installs into `%LOCALAPPDATA%\Programs\MarstekOverlay`: the data, the startup entry and the shortcuts are the user's own, and the app is never started elevated.
- **The startup entry** is the same value under `HKCU\…\Run` that the app's own setting keeps (`Services/Autostart`), in the same form, so the settings window shows it as on. Like the desktop shortcut it is ticked on the first install only: an update leaves both the way the user has them by then.
- **An update** asks the running copy to close through the Restart Manager — the regular way, which takes the icon out of the notification area and closes the database — and offers to start the app on the last page.
- **`appsettings.json` that is already there is kept**, since that is where a user switches a method off or changes the intervals. A key the kept file lacks takes the default the code has.
- **The uninstaller** stops the copy running from the install folder (a forced stop: it has no way to ask), removes the startup entry if it launches that copy, and asks whether to delete `%APPDATA%\MarstekOverlay` as well; a silent uninstall keeps the data.

The `AppId` in the script identifies the app to Windows across versions and must not change.

## Publishing a release

1. Raise `Version` in `src/Desktop/Desktop.csproj` and commit.
2. Tag the commit `v<version>` and push the tag:

   ```
   git tag v1.1.0
   git push origin v1.1.0
   ```

The `Release` workflow (`.github/workflows/release.yml`) runs the tests, builds the files with `Build-Release.ps1` and publishes them as a GitHub release with generated notes. The `CI` workflow builds and tests every push to `main` and every pull request.

The files are not signed, so SmartScreen warns about an unknown publisher until a version has gathered reputation.

<#
.SYNOPSIS
    Removes the copy of the app that Publish-App.ps1 installed, with everything it left behind.

.DESCRIPTION
    Stops the installed copy and removes its install directory, the startup entry and the shortcuts on the
    desktop and in the Start menu, the data of the app - the settings, the history and the log - the firewall
    rules made for it, and what Windows remembers of its icon in the notification area and of its notifications.

    The startup entry and the shortcuts go only when they launch the copy in the install directory: those of a
    copy installed elsewhere, by the installer of a release for one, stay.

    The data is looked for where the installed appsettings.json and the environment put it, so it goes only
    together with the install directory: once that is gone, the data may be that of another copy, and stays.
    The default folder, %APPDATA%\MarstekOverlay, is removed whole; a folder that Storage:RootDirectory names
    elsewhere loses only the files of the app and is removed if that leaves it empty. Another copy of the app
    that is running may hold those files, so the script asks for it to be closed first.

    Windows keeps the place of a notification-area icon for every path the app was ever started from. The
    records of the installed copy go, and so do those of copies that are no longer on the disk.

    Everything found is listed first and removed only once that is confirmed. The data cannot be brought back.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Remove-App.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Remove-App.ps1 -WhatIf

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Remove-App.ps1 -KeepData
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    # ProgramFiles is "Program Files (x86)" in a 32-bit PowerShell; ProgramW6432 is the 64-bit folder in both.
    [string]$InstallDirectory = (Join-Path $(if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }) 'MarstekOverlay'),

    # Leaves the settings, the history and the log where they are.
    [switch]$KeepData
)

$ErrorActionPreference = 'Stop'

# Under -WhatIf PowerShell skips every cmdlet that could change something, those that only ask Windows a
# question among them, and the script would find nothing. So the looking runs for real, and the removal is
# what -WhatIf stops.
$isDryRun = [bool]$WhatIfPreference
$WhatIfPreference = $false

$ExeName = 'MarstekOverlay.exe'

# What the exe says it is (Product in src/Desktop/Desktop.csproj). A portable exe of a release keeps the name of
# its file, so the running copies are told by this and not by the name of the process.
$ProductName = 'Marstek Overlay'

# What Publish-App.ps1 and src/Desktop/Services/Autostart.cs write, under the same names.
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ApprovalKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$EntryName = 'MarstekOverlay'
$ShortcutName = 'Marstek Overlay.lnk'

# Where Windows 11 keeps the icons of the notification area, a key per path of an exe, and the notification
# settings of each, under the name of that key.
$TrayIconsKeyPath = 'HKCU:\Control Panel\NotifyIconSettings'
$NotificationsKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings'
$NotificationHandlerPrefix = 'NotifyIconGeneratedAumid_'

# Relative to the current location of the session, not to the folder the process started in.
$InstallDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InstallDirectory).TrimEnd('\')
$InstalledExePath = Join-Path $InstallDirectory $ExeName
$DefaultDataDirectory = Join-Path ([System.Environment]::GetFolderPath('ApplicationData')) 'MarstekOverlay'

Add-Type -Namespace MarstekOverlayTools -Name Shell -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("shell32.dll")]
public static extern int SHGetKnownFolderPath(
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] System.Guid id,
    uint flags,
    System.IntPtr token,
    out System.IntPtr path);
'@

function Test-IsElevated {
    $principal = New-Object System.Security.Principal.WindowsPrincipal ([System.Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Whether this process runs as the account that owns the shell of its session.
function Test-IsShellUser {
    $session = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    $user = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value

    foreach ($shell in @(Get-CimInstance Win32_Process -Filter "Name = 'explorer.exe' AND SessionId = $session")) {
        if ((Invoke-CimMethod -InputObject $shell -MethodName GetOwnerSid).Sid -eq $user) { return $true }
    }

    return $false
}

function Test-IsSamePath([string]$first, [string]$second) {
    return [string]::Equals($first.TrimEnd('\'), $second.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-IsInside([string]$path, [string]$directory) {
    return $path.StartsWith("$directory\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-CanWriteTo([string]$directory) {
    try {
        $probe = Join-Path $directory ([System.IO.Path]::GetRandomFileName())
        [System.IO.File]::WriteAllText($probe, '')
        Remove-Item -LiteralPath $probe
        return $true
    }
    catch {
        return $false
    }
}

function Assert-IsAppDirectory([string]$directory) {
    if (-not (Test-Path -LiteralPath $directory)) { return }
    if (Test-Path -LiteralPath (Join-Path $directory $ExeName)) { return }
    if (-not (Get-ChildItem -LiteralPath $directory -Force | Select-Object -First 1)) { return }

    throw "$directory holds other files and no $ExeName; refusing to remove it."
}

# The installed copy starts without a command line, so its data lies where appsettings.json and the environment
# put it, and the environment wins.
function Get-Setting($settings, [string]$section, [string]$key, [string]$default) {
    $fromEnvironment = [System.Environment]::GetEnvironmentVariable("${section}__$key")
    if ($null -ne $fromEnvironment) { return $fromEnvironment }

    $values = $settings.$section
    if ($null -ne $values -and $null -ne $values.$key) { return [string]$values.$key }

    return $default
}

# What to remove of the data: a directory to remove whole, or the files of the app in a directory it shares.
# Resolved the way src/Desktop/Settings/StorageSettings.cs resolves it.
function Get-DataToRemove([string]$directory) {
    $settings = $null
    $appSettingsPath = Join-Path $directory 'appsettings.json'
    if (Test-Path -LiteralPath $appSettingsPath) {
        try {
            $settings = Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json
        }
        catch {
            Write-Warning "$appSettingsPath cannot be read ($($_.Exception.Message)); taking the data to be in $DefaultDataDirectory."
        }
    }

    $rootDirectory = Get-Setting $settings 'Storage' 'RootDirectory' ''
    $root = if ([string]::IsNullOrWhiteSpace($rootDirectory)) {
        $DefaultDataDirectory
    }
    else {
        [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($directory, $rootDirectory)).TrimEnd('\')
    }

    if ((Test-IsSamePath $root $directory) -or (Test-IsInside $root $directory)) {
        return @{ Directory = $null; Files = @(); Root = $root }
    }

    if (Test-IsSamePath $root $DefaultDataDirectory) {
        return @{ Directory = $(if (Test-Path -LiteralPath $root) { $root } else { $null }); Files = @(); Root = $root }
    }

    # The settings are saved through "<name>.tmp" and backed up as "<name>.bak"; the database keeps
    # "<stem>-log<extension>" beside it, and the log rolls over into "<stem>_001<extension>" and on.
    $configured = @(
        @{ Path = Get-Setting $settings 'Storage' 'UserSettingsFileName' 'settings.json'; HasSuffix = $true },
        @{ Path = Get-Setting $settings 'Storage' 'DatabaseFileName' 'history.db'; HasSuffix = $false },
        @{ Path = Get-Setting $settings 'LogConfiguration' 'LogFilePath' 'diagnostics.log'; HasSuffix = $false }
    )

    $files = foreach ($dataFile in $configured) {
        $path = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($root, $dataFile.Path))
        $folder = [System.IO.Path]::GetDirectoryName($path)
        $pattern = if ($dataFile.HasSuffix) {
            [System.IO.Path]::GetFileName($path) + '*'
        }
        else {
            [System.IO.Path]::GetFileNameWithoutExtension($path) + '*' + [System.IO.Path]::GetExtension($path)
        }

        if (Test-Path -LiteralPath $folder) {
            Get-ChildItem -LiteralPath $folder -Filter $pattern -File -Force | ForEach-Object FullName
        }
    }

    return @{ Directory = $null; Files = @($files | Sort-Object -Unique); Root = $root }
}

function Get-StartupEntry {
    $entry = (Get-ItemProperty -Path $RunKeyPath -ErrorAction SilentlyContinue).$EntryName
    if ($entry -and (Test-IsSamePath $entry.Trim('"') $InstalledExePath)) { return $entry }

    return $null
}

function Get-Shortcuts {
    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in [System.Environment]::GetFolderPath('Desktop'), [System.Environment]::GetFolderPath('Programs')) {
        $path = Join-Path $folder $ShortcutName
        if ((Test-Path -LiteralPath $path) -and (Test-IsSamePath $shell.CreateShortcut($path).TargetPath $InstalledExePath)) {
            $path
        }
    }
}

function Get-KnownFolder([guid]$id) {
    $pointer = [System.IntPtr]::Zero
    if ([MarstekOverlayTools.Shell]::SHGetKnownFolderPath($id, 0, [System.IntPtr]::Zero, [ref]$pointer) -ne 0) { return $null }

    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringUni($pointer)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::FreeCoTaskMem($pointer)
    }
}

# Windows writes a path under Program Files and the like as the id of that folder and the rest of the path.
function Resolve-TrayIconPath([string]$path) {
    if ($path -notmatch '^\{([0-9A-Fa-f-]{36})\}(\\.*)$') { return $path }

    $rest = $Matches[2]
    $folder = Get-KnownFolder ([guid]$Matches[1])
    if ($folder) { return $folder + $rest }

    return $null
}

# The icons of the installed copy, and of copies that are gone from the disk.
function Get-TrayIcons {
    if (-not (Test-Path $TrayIconsKeyPath)) { return }

    foreach ($key in Get-ChildItem $TrayIconsKeyPath) {
        $recorded = $key.GetValue('ExecutablePath')
        if (-not $recorded -or [System.IO.Path]::GetFileName($recorded) -ne $ExeName) { continue }

        $path = Resolve-TrayIconPath $recorded
        if ($path -and ((Test-IsSamePath $path $InstalledExePath) -or -not (Test-Path -LiteralPath $path))) {
            [pscustomobject]@{ Id = $key.PSChildName; ExePath = $path }
        }
    }
}

function Get-FirewallRules {
    if (-not (Get-Command Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue)) { return }

    try {
        Get-NetFirewallApplicationFilter -Program $InstalledExePath -ErrorAction Stop | Get-NetFirewallRule -ErrorAction Stop
    }
    catch {
        # The cmdlet reports a program without rules as an error.
    }
}

function Remove-Directory([string]$directory) {
    if (Test-CanWriteTo $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force -Confirm:$false
    }
    else {
        Write-Host "Asking for administrator rights to remove $directory"
        try {
            Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', "rd /s /q `"$directory`"" -Verb RunAs -WindowStyle Hidden -Wait
        }
        catch [System.InvalidOperationException] {
            throw "Removing $directory was not allowed to run elevated: $($_.Exception.Message)"
        }
    }

    if (Test-Path -LiteralPath $directory) {
        throw "$directory could not be removed whole; a program may hold a file in it."
    }
}

if ((Test-IsElevated) -and -not (Test-IsShellUser)) {
    throw 'The console runs as another account than the one signed in to this desktop: the startup entry, the shortcuts and the data of that account would be looked for instead. Run the script from a console of the signed-in user; it asks for administrator rights by itself when it needs them.'
}

Assert-IsAppDirectory $InstallDirectory

$session = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
$copies = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session -and $_.Product -eq $ProductName })
$running = @($copies | Where-Object { $_.Path -and (Test-IsSamePath $_.Path $InstalledExePath) })
$otherCopies = @($copies | Where-Object { $running -notcontains $_ })

$hasDirectory = Test-Path -LiteralPath $InstallDirectory
$startupEntry = Get-StartupEntry
$shortcuts = @(Get-Shortcuts)
$trayIcons = @(Get-TrayIcons)
$firewallRules = @(Get-FirewallRules)

# Without the install directory there is no telling whose the data is: a copy installed elsewhere, by the
# installer of a release for one, keeps its own in the same default folder.
$leavesData = $KeepData -or -not $hasDirectory
$data = if ($leavesData) { @{ Directory = $null; Files = @(); Root = $null } } else { Get-DataToRemove $InstallDirectory }

if (($data.Directory -or $data.Files) -and $otherCopies) {
    $named = $otherCopies | ForEach-Object { "  process $($_.Id), $(if ($_.Path) { $_.Path } else { 'path not available' })" }
    throw ("Another copy of the app is running and may hold the data in $($data.Root). Close it from its tray menu, " +
        "or pass -KeepData to leave the data alone.`nCopies running now:`n" + ($named -join "`n"))
}

$found = @()
foreach ($process in $running) { $found += "the running copy, process $($process.Id)" }
if ($hasDirectory) { $found += "the install directory $InstallDirectory" }
if ($startupEntry) { $found += "the startup entry $EntryName = $startupEntry" }
foreach ($shortcut in $shortcuts) { $found += "the shortcut $shortcut" }
if ($data.Directory) { $found += "the data folder $($data.Directory), with the settings, the history and the log" }
foreach ($file in $data.Files) { $found += "the data file $file" }
foreach ($rule in $firewallRules) { $found += "the firewall rule `"$($rule.DisplayName)`"" }
foreach ($icon in $trayIcons) { $found += "the notification-area icon and notification settings of $($icon.ExePath)" }

if (-not $found) {
    Write-Host "Nothing of the app installed in $InstallDirectory was found."
    return
}

Write-Host 'Found:'
$found | ForEach-Object { Write-Host "  $_" }
if ($KeepData) { Write-Host 'The data is left alone.' }
elseif ($leavesData) { Write-Host "$InstallDirectory is not there, so the data is left alone: it may be that of another copy." }

if ($isDryRun) {
    Write-Host 'What if: nothing was removed.'
    return
}

if (-not $PSCmdlet.ShouldProcess("everything listed above ($($found.Count) items)", 'Remove for good')) { return }

foreach ($process in $running) {
    Stop-Process -Id $process.Id -Force -Confirm:$false
    $process.WaitForExit(10000) | Out-Null
}

if ($startupEntry) {
    Remove-ItemProperty -Path $RunKeyPath -Name $EntryName -Confirm:$false
    if (Test-Path $ApprovalKeyPath) {
        Remove-ItemProperty -Path $ApprovalKeyPath -Name $EntryName -ErrorAction SilentlyContinue -Confirm:$false
    }
}

foreach ($shortcut in $shortcuts) {
    Remove-Item -LiteralPath $shortcut -Force -Confirm:$false
}

foreach ($icon in $trayIcons) {
    Remove-Item -LiteralPath (Join-Path $TrayIconsKeyPath $icon.Id) -Recurse -Force -Confirm:$false
    $handler = Join-Path $NotificationsKeyPath ($NotificationHandlerPrefix + $icon.Id)
    if (Test-Path -LiteralPath $handler) {
        Remove-Item -LiteralPath $handler -Recurse -Force -Confirm:$false
    }
}

foreach ($rule in $firewallRules) {
    try {
        $rule | Remove-NetFirewallRule -Confirm:$false
    }
    catch {
        Write-Warning "The firewall rule `"$($rule.DisplayName)`" was not removed ($($_.Exception.Message)); remove it from an elevated console."
    }
}

if ($data.Directory) {
    Remove-Item -LiteralPath $data.Directory -Recurse -Force -Confirm:$false
}

foreach ($file in $data.Files) {
    Remove-Item -LiteralPath $file -Force -Confirm:$false
}

if ($data.Files -and -not (Get-ChildItem -LiteralPath $data.Root -Force | Select-Object -First 1)) {
    Remove-Item -LiteralPath $data.Root -Force -Confirm:$false
}

# Last: the data is found through the appsettings.json in it, and a failure above leaves it to try again with.
if ($hasDirectory) {
    Remove-Directory $InstallDirectory
}

Write-Host "Removed. Nothing of the app installed in $InstallDirectory is left."

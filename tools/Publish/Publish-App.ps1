<#
.SYNOPSIS
    Publishes the app from the current sources and installs it for the signed-in user.

.DESCRIPTION
    Builds a Release publish into the "publish" folder at the repository root, mirrors it into the install
    directory, has Windows launch the installed copy at sign-in, puts shortcuts to it on the desktop and in
    the Start menu, and starts it.

    Only the copy into the install directory asks for administrator rights, and only when that directory is not
    writable: the startup entry and the shortcuts belong to the user who runs the script, and the app must not
    start as an administrator. A console elevated by the user signed in to this desktop is fine: the entry and
    the shortcuts are that user's all the same, and the app is started through the user's shell. A console
    elevated as another account is refused unless the script only publishes or is told to leave the startup
    entry, the shortcuts and the start alone.

    If the copy fails, the copy that was running before is started again. If the installed copy is not running
    a few seconds after its start, the script names the other copies of the app in this session: one of them
    keeps the data folder, and the installed copy only asked it to show its panel.

    The mirror leaves alone what the app keeps in the install directory when Storage:RootDirectory is relative:
    the files named by Storage and LogConfiguration in the published appsettings.json, and the folder that holds
    them.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Publish-App.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish/Publish-App.ps1 -PublishOnly
#>
param(
    # ProgramFiles is "Program Files (x86)" in a 32-bit PowerShell; ProgramW6432 is the 64-bit folder in both.
    [string]$InstallDirectory = (Join-Path $(if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }) 'MarstekOverlay'),

    # Stops after the "publish" folder is ready.
    [switch]$PublishOnly,

    # Leaves the startup entry as it is.
    [switch]$NoAutostart,

    # Creates no shortcuts and leaves existing ones as they are.
    [switch]$NoShortcuts,

    # Does not start the installed copy.
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

$ExeName = 'MarstekOverlay.exe'
$ProcessName = 'MarstekOverlay'

# The same value, in the same form, as src/Desktop/Services/Autostart.cs writes: the settings window shows
# the option as on only while the entry is exactly the quoted path of the running exe.
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ApprovalKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$EntryName = 'MarstekOverlay'

$ShortcutName = 'Marstek Overlay.lnk'

# Robocopy reports what it copied in the low bits; 8 and above mean that something failed.
$RobocopyFailure = 8

# The default names of what the app keeps in its storage folder, which lies next to the exe when
# Storage:RootDirectory is relative. The mirror neither copies nor deletes them, whatever appsettings.json names.
$DataFiles = 'settings.json*', 'history*.db', 'diagnostics*.log'

# A copy that finds another one on its data folder asks that one to show its panel and exits well within this.
$StartCheckSeconds = 8

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$PublishDirectory = Join-Path $RepositoryRoot 'publish'
$ProjectPath = Join-Path $RepositoryRoot 'src\Desktop\Desktop.csproj'

# Relative to the current location of the session, not to the folder the process started in.
$InstallDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InstallDirectory).TrimEnd('\')
$InstalledExePath = Join-Path $InstallDirectory $ExeName

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

# Returns whether a copy was running there.
function Stop-AppIn([string]$directory) {
    $exePath = Join-Path $directory $ExeName
    $running = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath })

    foreach ($process in $running) {
        Write-Host "Stopping $exePath (process $($process.Id))"
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(10000) | Out-Null
    }

    return $running.Count -gt 0
}

function Start-App([string]$exePath) {
    if (Test-IsElevated) {
        # Explorer hands the start to the shell of the signed-in user, which does not pass the elevation on.
        Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$exePath`""
    }
    else {
        Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath)
    }
}

# Copies of other users are left out: one of them cannot hold the data folder of this one.
function Get-SessionCopies {
    $session = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    return @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session })
}

# Other copies are only named, never stopped: a check of the script on a temporary install directory would
# otherwise stop the copy the user runs.
function Confirm-IsRunning([string]$exePath) {
    Start-Sleep -Seconds $StartCheckSeconds

    $copies = Get-SessionCopies
    if ($copies | Where-Object { $_.Path -eq $exePath }) { return }

    $others = foreach ($copy in $copies) {
        if ($copy.Path) {
            "  $($copy.Path), version $((Get-Item -LiteralPath $copy.Path).VersionInfo.FileVersion), process $($copy.Id)"
        }
        else {
            "  process $($copy.Id), path not available"
        }
    }

    if ($others) {
        Write-Warning ("$exePath is not running $StartCheckSeconds seconds after its start. Another copy of the app " +
            "probably holds the same data folder, and the installed copy only asked it to show its panel. Close " +
            "that copy from its tray menu and start $exePath.`nCopies running now:`n" + ($others -join "`n"))
    }
    else {
        Write-Warning "$exePath is not running $StartCheckSeconds seconds after its start. Its log in the data folder may say why."
    }
}

function Test-CanWriteTo([string]$directory) {
    try {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        $probe = Join-Path $directory ([System.IO.Path]::GetRandomFileName())
        [System.IO.File]::WriteAllText($probe, '')
        Remove-Item -LiteralPath $probe
        return $true
    }
    catch {
        # Whatever stood in the way, the elevated copy either gets past it or names it.
        return $false
    }
}

function Assert-IsAppDirectory([string]$directory) {
    if (-not (Test-Path -LiteralPath $directory)) { return }
    if (Test-Path -LiteralPath (Join-Path $directory $ExeName)) { return }
    if (-not (Get-ChildItem -LiteralPath $directory -Force | Select-Object -First 1)) { return }

    # The copy is a mirror: everything the publish does not contain is deleted from the target.
    throw "$directory holds other files and no $ExeName; refusing to mirror the app into it."
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

# Name patterns for the data files that lie right in the install directory, and the folders right below it
# that hold data deeper down, as the published appsettings.json places them; resolved the way
# src/Desktop/Settings/StorageSettings.cs resolves them.
function Get-DataExclusions([string]$source, [string]$target) {
    $appSettingsPath = Join-Path $source 'appsettings.json'
    try {
        $settings = Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Cannot read $appSettingsPath to tell which files in $target hold data: $($_.Exception.Message)"
    }

    $rootDirectory = Get-Setting $settings 'Storage' 'RootDirectory' ''
    $root = if ([string]::IsNullOrWhiteSpace($rootDirectory)) {
        Join-Path ([System.Environment]::GetFolderPath('ApplicationData')) 'MarstekOverlay'
    }
    else {
        [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($target, $rootDirectory))
    }

    # The settings are saved through "<name>.tmp" and backed up as "<name>.bak"; the database keeps
    # "<stem>-log<extension>" beside it, and the log rolls over into "<stem>_001<extension>" and on.
    $configured = @(
        @{ Path = Get-Setting $settings 'Storage' 'UserSettingsFileName' 'settings.json'; HasSuffix = $true },
        @{ Path = Get-Setting $settings 'Storage' 'DatabaseFileName' 'history.db'; HasSuffix = $false },
        @{ Path = Get-Setting $settings 'LogConfiguration' 'LogFilePath' 'diagnostics.log'; HasSuffix = $false }
    )

    $files = @($DataFiles)
    $directories = @()
    foreach ($dataFile in $configured) {
        $path = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($root, $dataFile.Path))
        $directory = [System.IO.Path]::GetDirectoryName($path)

        if ($directory -eq $target) {
            $files += if ($dataFile.HasSuffix) {
                [System.IO.Path]::GetFileName($path) + '*'
            }
            else {
                [System.IO.Path]::GetFileNameWithoutExtension($path) + '*' + [System.IO.Path]::GetExtension($path)
            }
        }
        elseif ($directory.StartsWith("$target\", [System.StringComparison]::OrdinalIgnoreCase)) {
            $directories += Join-Path $target ($directory.Substring($target.Length + 1).Split('\')[0])
        }
    }

    $files = @($files | Sort-Object -Unique)
    $directories = @($directories | Sort-Object -Unique)

    # Whatever matches is neither copied nor deleted, so a shipped file taken for data would never be updated.
    foreach ($directory in $directories) {
        if (Test-Path -LiteralPath (Join-Path $source (Split-Path $directory -Leaf))) {
            throw "The data folder $directory is also a folder of the app; refusing to mirror into $target."
        }
    }

    $shipped = @(Get-ChildItem -LiteralPath $source -Recurse -File -Force)
    foreach ($pattern in $files) {
        $clash = $shipped | Where-Object { $_.Name -like $pattern } | Select-Object -First 1
        if ($clash) {
            throw "$($clash.FullName) matches the data files $pattern and would never be updated; refusing to mirror into $target."
        }
    }

    return @{ Files = $files; Directories = $directories }
}

function Copy-Publish([string]$source, [string]$target) {
    $data = Get-DataExclusions $source $target
    $options = @('/MIR', '/R:5', '/W:1', '/NP', '/NFL', '/NDL', '/NJH', '/NJS', '/XF') + $data.Files
    if ($data.Directories) {
        $options += @('/XD') + $data.Directories
    }

    foreach ($directory in $data.Directories) {
        Write-Host "Leaving $directory as it is: it holds the data of the app"
    }

    if (Test-CanWriteTo $target) {
        & robocopy.exe $source $target @options | Out-Null
        $exitCode = $LASTEXITCODE
    }
    else {
        Write-Host "Asking for administrator rights to write to $target"

        # Start-Process joins the arguments as they are, so the paths carry their own quotes.
        $arguments = @("`"$source`"", "`"$target`"") + ($options | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } })
        try {
            $copy = Start-Process -FilePath 'robocopy.exe' -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        }
        catch [System.InvalidOperationException] {
            throw "The copy into $target was not allowed to run elevated: $($_.Exception.Message)"
        }

        $exitCode = $copy.ExitCode
    }

    if ($exitCode -ge $RobocopyFailure) {
        throw "robocopy could not mirror $source into $target (exit code $exitCode)."
    }
}

function Enable-Autostart([string]$exePath) {
    if (-not (Test-Path $RunKeyPath)) {
        New-Item -Path $RunKeyPath -Force | Out-Null
    }

    Set-ItemProperty -Path $RunKeyPath -Name $EntryName -Value "`"$exePath`""

    # A mark left by Task Manager would keep the entry from launching however often it is written anew.
    if (Test-Path $ApprovalKeyPath) {
        Remove-ItemProperty -Path $ApprovalKeyPath -Name $EntryName -ErrorAction SilentlyContinue
    }
}

function Set-Shortcut([string]$directory, [string]$exePath) {
    $shortcutPath = Join-Path $directory $ShortcutName
    $shell = New-Object -ComObject WScript.Shell

    # Saving over an existing shortcut rewrites its target and keeps where the user put its icon.
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = Split-Path $exePath
    $shortcut.Save()

    Write-Host "Shortcut: $shortcutPath"
}

$IsForSignedInUser = -not ($PublishOnly -or ($NoAutostart -and $NoShortcuts -and $NoStart))
if ($IsForSignedInUser -and (Test-IsElevated) -and -not (Test-IsShellUser)) {
    throw 'The console runs as another account than the one signed in to this desktop: the startup entry and the shortcuts would belong to that account. Run the script from a console of the signed-in user; the copy asks for administrator rights by itself when it needs them.'
}

Stop-AppIn $PublishDirectory | Out-Null

if (Test-Path -LiteralPath $PublishDirectory) {
    Remove-Item -LiteralPath $PublishDirectory -Recurse -Force
}

& dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false -o $PublishDirectory --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published to $PublishDirectory"

if ($PublishOnly) { return }

Assert-IsAppDirectory $InstallDirectory
$wasRunning = Stop-AppIn $InstallDirectory
try {
    Copy-Publish $PublishDirectory $InstallDirectory
}
catch {
    if ($wasRunning -and (Test-Path -LiteralPath $InstalledExePath)) {
        Start-App $InstalledExePath
        Write-Host "The copy failed; started $InstalledExePath again"
    }

    throw
}

Write-Host "Installed to $InstallDirectory"

if (-not $NoAutostart) {
    Enable-Autostart $InstalledExePath
    Write-Host 'Windows will start the installed copy at sign-in'
}

if (-not $NoShortcuts) {
    Set-Shortcut ([System.Environment]::GetFolderPath('Desktop')) $InstalledExePath
    Set-Shortcut ([System.Environment]::GetFolderPath('Programs')) $InstalledExePath
}

if (-not $NoStart) {
    Start-App $InstalledExePath
    Write-Host "Started $InstalledExePath"
    Confirm-IsRunning $InstalledExePath
}

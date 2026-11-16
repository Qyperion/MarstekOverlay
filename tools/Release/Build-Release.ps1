<#
.SYNOPSIS
    Builds the files of a release: the installer and the two portable exes.

.DESCRIPTION
    Publishes the app three times into artifacts/publish: as one exe that needs the .NET Desktop Runtime, as
    one exe with the runtime inside ("self-contained"), and as a folder with the runtime inside, which Inno
    Setup builds the installer from. The three exes land in artifacts/release, with their SHA-256 sums beside
    them.

    The version is the one in src/Desktop/Desktop.csproj. The release workflow passes the version of the tag
    it runs for, and a tag that names another version than the project is refused.

    The installer needs Inno Setup 6.3 or later (https://jrsoftware.org/isinfo.php).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Release/Build-Release.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/Release/Build-Release.ps1 -SkipInstaller
#>
param(
    # The version the release is meant to carry, with or without a leading "v"; refused when the project says otherwise.
    [string]$Version,

    # Builds the portable exes alone, on a machine without Inno Setup.
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'

$Runtime = 'win-x64'
$ExeName = 'MarstekOverlay.exe'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$ProjectPath = Join-Path $RepositoryRoot 'src\Desktop\Desktop.csproj'
$InstallerScript = Join-Path $RepositoryRoot 'tools\Installer\MarstekOverlay.iss'
$PublishRoot = Join-Path $RepositoryRoot 'artifacts\publish'
$ReleaseDirectory = Join-Path $RepositoryRoot 'artifacts\release'

# What the installer puts beside the app: its licence and those of the libraries inside it.
$Notices = 'LICENSE', 'THIRD-PARTY-NOTICES.md'

$OneFile = '-p:PublishSingleFile=true'

# With the runtime inside, an exe of one file cannot start on .NET 11.0: two assemblies of WPF hold native
# code and cannot be loaded from inside the exe (https://github.com/dotnet/wpf/issues/11842). Until that is
# fixed the exe unpacks everything into %TEMP%\.net on its first start and runs from there, which the SDK
# calls a legacy mode and warns about. Compressed, it is under half the size.
$OneFileWithRuntime = $OneFile, '-p:IncludeAllContentForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-warnAsMessage:NETSDK1244'

function Get-ProjectVersion {
    $versions = @(([xml](Get-Content -LiteralPath $ProjectPath -Raw)).Project.PropertyGroup.Version | Where-Object { $_ })
    if ($versions.Count -ne 1) {
        throw "$ProjectPath is expected to name exactly one Version, found $($versions.Count)."
    }

    return $versions[0]
}

function Find-InnoSetup {
    $onPath = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $folders = ${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')
    foreach ($folder in $folders | Where-Object { $_ }) {
        $compiler = Join-Path $folder 'Inno Setup 6\ISCC.exe'
        if (Test-Path -LiteralPath $compiler) { return $compiler }
    }

    throw 'Inno Setup 6 was not found (ISCC.exe). Install it from https://jrsoftware.org/isinfo.php, or pass -SkipInstaller to build the portable exes alone.'
}

# Returns the folder the app was published into.
function Publish-App([string]$name, [bool]$withRuntime, [string[]]$options) {
    $directory = Join-Path $PublishRoot $name

    # The symbols go inside the assemblies: a failure in the log keeps its line numbers, and no .pdb is shipped.
    & dotnet publish $ProjectPath -c Release -r $Runtime --self-contained $withRuntime.ToString().ToLowerInvariant() `
        -o $directory --nologo -p:DebugType=embedded @options | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish of $name failed with exit code $LASTEXITCODE."
    }

    return $directory
}

# An exe of one file is the whole app: it runs on the defaults of the code, with no appsettings.json beside it.
function Copy-PortableExe([string]$directory, [string]$name) {
    $exe = Join-Path $ReleaseDirectory $name
    Copy-Item -LiteralPath (Join-Path $directory $ExeName) -Destination $exe
    Write-Host "Portable exe: $exe"
}

$projectVersion = Get-ProjectVersion
if ($Version -and $Version.TrimStart('v') -ne $projectVersion) {
    throw "The release is meant to be $Version, but $ProjectPath says $projectVersion. Change the Version there first."
}

# Looked for before the long part, not after it.
$compiler = if ($SkipInstaller) { $null } else { Find-InnoSetup }

foreach ($directory in $PublishRoot, $ReleaseDirectory) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

New-Item -ItemType Directory -Force -Path $ReleaseDirectory | Out-Null

$portableDirectory = Publish-App $Runtime $false $OneFile
$portableWithRuntimeDirectory = Publish-App "$Runtime-self-contained" $true $OneFileWithRuntime

Copy-PortableExe $portableDirectory "MarstekOverlay-$projectVersion-$Runtime.exe"
Copy-PortableExe $portableWithRuntimeDirectory "MarstekOverlay-$projectVersion-$Runtime-self-contained.exe"

if ($compiler) {
    # A folder rather than one exe: installed, the app starts without unpacking anything.
    $installedDirectory = Publish-App 'installer' $true @()
    foreach ($notice in $Notices) {
        Copy-Item -LiteralPath (Join-Path $RepositoryRoot $notice) -Destination $installedDirectory
    }

    & $compiler /Qp "/DAppVersion=$projectVersion" "/DSourceDir=$installedDirectory" "/DOutputDir=$ReleaseDirectory" $InstallerScript | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    Write-Host "Installer: $(Join-Path $ReleaseDirectory "MarstekOverlay-Setup-$projectVersion.exe")"
}

$sums = foreach ($file in Get-ChildItem -LiteralPath $ReleaseDirectory -File | Sort-Object Name) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name
}

Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt') -Value $sums -Encoding ASCII

Write-Host "Release $projectVersion is in $ReleaseDirectory"

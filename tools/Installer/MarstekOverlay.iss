; The installer of Marstek Overlay, for Inno Setup 6.3 or later. tools/Release/Build-Release.ps1 runs it and
; passes the version and the folders:
;   ISCC /DAppVersion=1.0.0 /DSourceDir=<published app> /DOutputDir=<where the installer goes> MarstekOverlay.iss
;
; The file is UTF-8 with a byte order mark: without the mark Inno Setup reads the Ukrainian texts as ANSI.

#ifndef AppVersion
  #error AppVersion is not given: /DAppVersion=1.0.0
#endif
#ifndef SourceDir
  #error SourceDir is not given: /DSourceDir=<folder of the published app>
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

#define AppName "Marstek Overlay"
#define AppExeName "MarstekOverlay.exe"
#define AppProcessName "MarstekOverlay"
#define AppUrl "https://github.com/Qyperion/MarstekOverlay"
#define SettingsFile "appsettings.json"

; The entry src/Desktop/Services/Autostart.cs keeps, under the same name and in the same form: the settings
; window shows the option as on only while the value is exactly the quoted path of the running exe.
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#define ApprovalKey "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
#define RunValue "MarstekOverlay"

[Setup]
; Identifies the app to Windows across versions; changing it would install a second copy beside the first.
AppId={{A283F746-E94B-4644-96AA-CCAB24846EE1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Qyperion
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; For the current user alone: the data, the startup entry and the shortcuts are that user's, so nothing here
; needs an administrator, and the app is never started as one.
PrivilegesRequired=lowest
DefaultDirName={autopf}\MarstekOverlay
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

WizardStyle=modern
SetupIconFile=..\..\src\Desktop\Assets\App.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=MarstekOverlay-Setup-{#AppVersion}

; A running copy is asked to close the regular way, which takes its icon out of the notification area and
; closes the database. It is started again by the last page of the wizard, not behind the user's back.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[CustomMessages]
en.StartWithWindows=Start with Windows
uk.StartWithWindows=Запускати разом із Windows
en.DeleteData=Delete the settings and the history in %1 as well?
uk.DeleteData=Видалити також налаштування й історію в %1?

[Tasks]
; Ticked on the first install only. An update leaves the shortcut and the startup entry the way the user has
; them by then: the entry may have been switched off in the app's own settings.
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "autostart"; Description: "{cm:StartWithWindows}"; Flags: checkedonce

[Files]
; The user sets the poll intervals and the methods never sent in this file, so one that is there is kept.
Source: "{#SourceDir}\{#SettingsFile}"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "\{#SettingsFile}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#RunValue}"; ValueData: """{app}\{#AppExeName}"""; Tasks: autostart
; A mark left by Task Manager would keep the entry from launching however often it is written anew.
Root: HKCU; Subkey: "{#ApprovalKey}"; ValueType: none; ValueName: "{#RunValue}"; Flags: deletevalue dontcreatekey; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function InstalledExe: String;
begin
  Result := ExpandConstant('{app}\{#AppExeName}');
end;

// The uninstaller has no way to ask the app to close, and cannot delete an exe that is running. Copies in
// other folders are left alone.
procedure StopInstalledCopy;
var
  Path, Command: String;
  ExitCode: Integer;
begin
  Path := InstalledExe;
  StringChangeEx(Path, '''', '''''', True);

  Command := '-NoProfile -ExecutionPolicy Bypass -Command "' +
    '$copies = @(Get-Process {#AppProcessName} -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq ''' + Path + ''' }); ' +
    'if ($copies) { $copies | Stop-Process -Force; $copies | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue }"';

  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Command, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
end;

// The entry is the app's own to switch on, so the uninstaller cannot count on having written it. One that
// launches a copy in another folder stays.
procedure RemoveStartupEntry;
var
  Entry: String;
begin
  if RegQueryStringValue(HKCU, '{#RunKey}', '{#RunValue}', Entry)
    and (CompareText(Entry, '"' + InstalledExe + '"') = 0) then
  begin
    RegDeleteValue(HKCU, '{#RunKey}', '{#RunValue}');
    RegDeleteValue(HKCU, '{#ApprovalKey}', '{#RunValue}');
  end;
end;

// Only the default data folder is offered: one that appsettings.json placed elsewhere is not the installer's to find.
procedure OfferToDeleteData;
var
  DataDirectory: String;
begin
  DataDirectory := ExpandConstant('{userappdata}\MarstekOverlay');
  if UninstallSilent or not DirExists(DataDirectory) then
    Exit;

  if MsgBox(FmtMessage(CustomMessage('DeleteData'), [DataDirectory]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(DataDirectory, True, True, True);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopInstalledCopy
  else if CurUninstallStep = usPostUninstall then
  begin
    RemoveStartupEntry;
    OfferToDeleteData;
  end;
end;

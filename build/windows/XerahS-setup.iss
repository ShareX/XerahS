#define MyAppName "XerahS"
#define MyAppExeName "XerahS.exe"
#define MyAppRootDirectory "..\..\..\"
#ifndef MyAppReleaseDirectory
#define MyAppReleaseDirectory MyAppRootDirectory + "src\\desktop\\app\\XerahS.App\\bin\\Release\\net10.0-windows10.0.26100.0"
#endif

#ifndef MyAppPublisher
#define MyAppPublisher "ShareX Team"
#endif

#ifndef MyAppURL
#define MyAppURL "https://github.com/ShareX/XerahS"
#endif

#ifndef MyAppVersion
#define MyAppVersion GetStringFileInfo(MyAppReleaseDirectory + "\" + MyAppExeName, "ProductVersion")
#endif

#ifndef MyAppFileVersion
#define MyAppFileVersion GetStringFileInfo(MyAppReleaseDirectory + "\" + MyAppExeName, "FileVersion")
#endif

#ifndef MyAppShortVersion
#define MyAppShortVersion Copy(MyAppFileVersion, 1, RPos(".", MyAppFileVersion) - 1)
#endif

#ifndef MyAppId
#define MyAppId "7B28B84B-3D6B-4198-8424-95C4F6298517"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename MyAppName + "-" + MyAppShortVersion + "-setup"
#endif

#ifndef OutputDir
#define OutputDir MyAppRootDirectory + "Output"
#endif

[Setup]
AppCopyright=Copyright (c) 2007-2026 ShareX Team
AppId={#MyAppId}
AppMutex={#MyAppId}
AppName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppVerName={#MyAppName} {#MyAppShortVersion}
AppVersion={#MyAppFileVersion}
ArchitecturesAllowed=x64 arm64
ArchitecturesInstallIn64BitMode=x64 arm64
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile={#MyAppRootDirectory}\XerahS\LICENSE.txt
MinVersion=10.0.17763
OutputBaseFilename={#OutputBaseFilename}
OutputDir={#OutputDir}
PrivilegesRequired=lowest
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoVersion={#MyAppFileVersion}

[Tasks]
Name: "CreateDesktopIcon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Check: not IsUpdating and not DesktopIconExists
Name: "CreateStartupIcon"; Description: "Run {#MyAppName} when Windows starts"; GroupDescription: "Other tasks:"; Check: not IsUpdating

[Files]
Source: "{#MyAppReleaseDirectory}\{#MyAppExeName}"; DestDir: {app}; Flags: ignoreversion
Source: "{#MyAppReleaseDirectory}\xerahs-watchfolder-daemon.exe"; DestDir: {app}; Flags: ignoreversion
Source: "{#MyAppReleaseDirectory}\xerahs-watchfolder-daemon.deps.json"; DestDir: {app}; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#MyAppReleaseDirectory}\xerahs-watchfolder-daemon.runtimeconfig.json"; DestDir: {app}; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#MyAppReleaseDirectory}\*.dll"; DestDir: {app}; Flags: ignoreversion
Source: "{#MyAppReleaseDirectory}\*.resources.dll"; DestDir: {app}; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#MyAppReleaseDirectory}\*.json"; DestDir: {app}; Flags: ignoreversion
Source: "{#MyAppReleaseDirectory}\runtimes\win-x64\*"; DestDir: "{app}\runtimes\win-x64"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#MyAppReleaseDirectory}\runtimes\win-arm64\*"; DestDir: "{app}\runtimes\win-arm64"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#MyAppReleaseDirectory}\WebUI\dist\*"; DestDir: "{app}\WebUI\dist"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#MyAppReleaseDirectory}\Plugins\*"; DestDir: "{userdocs}\{#MyAppName}\Plugins"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Remove the obsolete shortcut created when the Video Editor inherited its shared ShareX window title.
Type: files; Name: "{userprograms}\ShareX — Video Editor.lnk"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: CreateDesktopIcon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" -silent"; Tasks: CreateStartupIcon; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\xerahs"; ValueType: string; ValueName: ""; ValueData: "URL:XerahS OAuth Callback"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\xerahs"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\xerahs\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\xerahs\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall; Check: not IsNoRun

[Code]
var
  DaemonWasRunning: Boolean;

function CmdLineParamExists(const value: string): Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 1 to ParamCount do
    if CompareText(ParamStr(i), value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function IsUpdating(): Boolean;
begin
  Result := CmdLineParamExists('/UPDATE');
end;

function IsNoRun(): Boolean;
begin
  Result := CmdLineParamExists('/NORUN');
end;

function DesktopIconExists(): Boolean;
begin
  Result := FileExists(ExpandConstant('{userdesktop}\{#MyAppName}.lnk'));
end;

procedure StopWatchFolderDaemon();
var
  ResultCode: Integer;
begin
  DaemonWasRunning := False;
  { Graceful shutdown first: sends WM_CLOSE / CTRL_BREAK without /F }
  if Exec('taskkill.exe', '/IM xerahs-watchfolder-daemon.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      DaemonWasRunning := True;
      Sleep(2000); { Give the daemon time to exit cleanly }
    end;
  end;
  { Safety net: force-terminate any remaining instance }
  Exec('taskkill.exe', '/F /IM xerahs-watchfolder-daemon.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if DaemonWasRunning then
    Sleep(500); { Brief pause for OS to release file handles }
end;

procedure StartWatchFolderDaemon();
var
  ResultCode: Integer;
begin
  if FileExists(ExpandConstant('{app}\xerahs-watchfolder-daemon.exe')) then
    Exec(ExpandConstant('{app}\xerahs-watchfolder-daemon.exe'), '', ExpandConstant('{app}'), SW_HIDE, ewNoWait, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  StopWatchFolderDaemon();
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Only restart the daemon if it was running before the install }
  if (CurStep = ssPostInstall) and DaemonWasRunning then
    StartWatchFolderDaemon();
end;

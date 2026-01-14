#define MyAppName "XerahS"
#define MyAppExeName "XerahS.exe"
#define MyAppRootDirectory "..\..\"
#define MyAppReleaseDirectory MyAppRootDirectory + "ShareX.Avalonia\src\ShareX.Avalonia.App\bin\Release\net10.0-windows10.0.26100.0"
#define MyAppPublisher "ShareX Team"
#define MyAppURL "https://github.com/ShareX/ShareX.Avalonia"
#define MyAppVersion GetStringFileInfo(MyAppReleaseDirectory + "\" + MyAppExeName, "ProductVersion")
#define MyAppFileVersion GetStringFileInfo(MyAppReleaseDirectory + "\" + MyAppExeName, "FileVersion")
#define MyAppShortVersion Copy(MyAppFileVersion, 1, RPos(".", MyAppFileVersion) - 1)
#define MyAppId "7B28B84B-3D6B-4198-8424-95C4F6298517"

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
LicenseFile={#MyAppRootDirectory}\ShareX.Avalonia\LICENSE.txt
MinVersion=10.0.17763
OutputBaseFilename={#MyAppName}-{#MyAppShortVersion}-setup
OutputDir={#MyAppRootDirectory}\Output
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
Source: "{#MyAppReleaseDirectory}\*.dll"; DestDir: {app}; Flags: ignoreversion
Source: "{#MyAppReleaseDirectory}\*.json"; DestDir: {app}; Flags: ignoreversion
Source: "{#MyAppReleaseDirectory}\runtimes\win-x64\*"; DestDir: "{app}\runtimes\win-x64"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyAppReleaseDirectory}\runtimes\win-arm64\*"; DestDir: "{app}\runtimes\win-arm64"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyAppReleaseDirectory}\Plugins\*"; DestDir: "{userdocs}\{#MyAppName}\Plugins"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: CreateDesktopIcon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Parameters: "-silent"; Tasks: CreateStartupIcon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall; Check: not IsNoRun

[Code]
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

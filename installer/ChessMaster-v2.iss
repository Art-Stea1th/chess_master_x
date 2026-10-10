; Silent engine for the Chess Master shell installer.
; The shell draws the window. This only copies files into the user profile.

#define MyAppName "Chess Master"
#define MyAppVersion "1.0.2"
#define MyAppExe "ChessMaster.exe"

[Setup]
AppId={{A7C3E1B4-8F2D-4C6A-9B15-2D4E6F8A0C31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion=1.0.2.0
VersionInfoDescription=Desktop shell for chess.com
DefaultDirName={localappdata}\Programs\Chess Master
DisableDirPage=yes
UsePreviousAppDir=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=engine
OutputBaseFilename=ChessMaster-Engine-1.0.2
SetupIconFile=..\src\ChessMaster\Assets\knight.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
AppMutex=Local\ChessMaster.Shell
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ProgressPath: String;
  LastReported: Integer;

function WebView2Installed: Boolean;
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
  if Result then
    Result := (Version <> '') and (Version <> '0.0.0.0');
end;

procedure WriteProgress(Percent: Integer);
begin
  if (ProgressPath = '') or (Percent = LastReported) then
    Exit;
  if Percent < 0 then
    Percent := 0;
  if Percent > 100 then
    Percent := 100;
  LastReported := Percent;
  SaveStringToFile(ProgressPath, IntToStr(Percent), False);
end;

function InitializeSetup: Boolean;
begin
  ProgressPath := ExpandConstant('{param:PROGRESS|}');
  LastReported := -1;
  Result := True;
  if WizardSilent then
    Exit;
  if not WebView2Installed then
    MsgBox('Не найден Microsoft Edge WebView2 Runtime. Установщик прав администратора не просит. Chess Master поставится, но страница откроется только после установки WebView2 Runtime.', mbInformation, MB_OK);
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
  if MaxProgress <= 0 then
    Exit;
  WriteProgress(CurProgress * 100 div MaxProgress);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteProgress(100);
end;

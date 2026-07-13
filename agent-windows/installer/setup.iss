[Setup]
AppName=AMHARC Match Capture
AppVersion=0.1.0-alpha
AppPublisher=AMHARC
AppPublisherURL=https://github.com/fishnany/amharc-match-capture
AppSupportURL=https://github.com/fishnany/amharc-match-capture/issues
DefaultDirName={autopf}\AMHARC Match Capture
DefaultGroupName=AMHARC Match Capture
OutputBaseFilename=amharc-match-capture-setup
OutputDir=..\dist
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.19041
; Place amharc.ico alongside setup.iss before building
; SetupIconFile=amharc.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"
Name: "autostart"; Description: "Start AMHARC Agent &automatically with Windows"; GroupDescription: "Startup options:"

[Files]
; Published application (run: dotnet publish -c Release -r win-x64 --self-contained true)
Source: "..\src\AmharcAgent.Api\bin\Release\net8.0-windows\win-x64\publish\*"; \
  DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Bundled FFmpeg (download from https://ffmpeg.org/download.html, place ffmpeg.exe here)
Source: "ffmpeg\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion

; Pre-built operator UI (run: pnpm --filter @workspace/operator-ui run build)
Source: "..\src\AmharcAgent.Api\wwwroot\*"; \
  DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AMHARC Match Capture"; Filename: "{app}\AmharcAgent.Api.exe"
Name: "{group}\Open Operator Interface"; Filename: "http://localhost:5000"
Name: "{group}\Uninstall AMHARC Match Capture"; Filename: "{uninstallexe}"
Name: "{commondesktop}\AMHARC Match Capture"; Filename: "{app}\AmharcAgent.Api.exe"; \
  Tasks: desktopicon

[Registry]
; Auto-start with Windows
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "AmharcAgent"; \
  ValueData: """{app}\AmharcAgent.Api.exe"""; \
  Flags: uninsdeletevalue; Tasks: autostart

; Firewall rule so the browser can reach localhost:5000 (optional — localhost is usually allowed)
; Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\..."; ...

[Run]
Filename: "{app}\AmharcAgent.Api.exe"; Description: "Start AMHARC Agent"; \
  Flags: nowait postinstall skipifsilent
Filename: "http://localhost:5000"; Description: "Open &Operator Interface in browser"; \
  Flags: shellexec postinstall skipifsilent

[UninstallRun]
; Stop the agent process on uninstall
Filename: "taskkill"; Parameters: "/IM AmharcAgent.Api.exe /F"; \
  Flags: runhidden; RunOnceId: "StopAgent"

[Code]
var
  ResultCode: Integer;

function IsDotNet8Installed: Boolean;
var
  Versions: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App',
    Versions) then
  begin
    for I := 0 to GetArrayLength(Versions) - 1 do
      if Pos('8.', Versions[I]) = 1 then
      begin
        Result := True;
        Break;
      end;
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsDotNet8Installed then
  begin
    if MsgBox(
      '.NET 8 Runtime is required but was not found on this machine.' + #13#10 +
      'Would you like to open the .NET download page now?' + #13#10 + #13#10 +
      'After installing .NET 8, run this setup again.',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open',
        'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
        '', '', SW_SHOW, ewNoWait, ResultCode);
    end;
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create recording directory
    CreateDir('C:\AmharcRecordings');
  end;
end;

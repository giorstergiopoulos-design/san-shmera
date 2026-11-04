; Inno Setup script for Chronikologio (also compiles with 7.x). Build with build-installer.bat, which publishes into dist\ first.
;
; Components:
;   app     the stand-alone Chronikologio window (Start menu entry, optional desktop icon)
;   plugin  the Opsis plugin: copies the plugin into %AppData%\Opsis\plugins\Chronikologio (a folder Opsis scans at start-up).
;           Opsis then shows the Chronikologio calendar widget instead of its built-in one and offers the "Σαν σήμερα" feed for the bars.
; Silent install:  ChronikologioSetup.exe /VERYSILENT /COMPONENTS="app,plugin"
#define MyAppName "Χρονικολόγιο"
#define MyAppVersion "0.4.0"
#define MyAppExe "Chronikologio.exe"
#ifndef DistDir
  #define DistDir "..\dist"
#endif

[Setup]
AppId={{B7C4E2A1-5D3F-4E8A-9C61-2F0A8D7E4B93}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Chronikologio
DefaultDirName={autopf64}\Chronikologio
DefaultGroupName={#MyAppName}
OutputDir=..\Output
OutputBaseFilename=ChronikologioSetup
Compression=lzma2/ultra64
LZMADictionarySize=262144
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=2
SolidCompression=yes
; The application goes to C:\Program Files\Chronikologio, which needs administrator rights (with "lowest" {autopf} would be a folder under AppData).
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
SetupIconFile=..\src\Chronikologio.Windows\chronikologio.ico
; The running programs that hold the files are closed by the [Code] section below (Opsis loads the plugins into its own process), without a question.
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

[Languages]
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Πλήρης εγκατάσταση"
Name: "custom"; Description: "Προσαρμοσμένη εγκατάσταση"; Flags: iscustom

[Components]
Name: "app"; Description: "Εφαρμογή Χρονικολόγιο"; Types: full custom; Flags: fixed
Name: "plugin"; Description: "Plugin για το Opsis (ημερολόγιο widget και ροή «Σαν σήμερα»). Απαιτεί επανεκκίνηση του Opsis."; Types: full

[Tasks]
Name: "desktopicon"; Description: "Δημιουργία συντόμευσης στην επιφάνεια εργασίας"; Components: app; Flags: unchecked

[Files]
Source: "{#DistDir}\app\*"; DestDir: "{app}"; Components: app; Flags: recursesubdirs ignoreversion
; The plugin is the one part that must live in AppData: Opsis scans %AppData%\Opsis\plugins at start-up for each user.
Source: "{#DistDir}\plugin\*"; DestDir: "{userappdata}\Opsis\plugins\Chronikologio"; Components: plugin; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Components: app
Name: "{group}\Απεγκατάσταση"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Components: app

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Εκκίνηση του Χρονικολόγιου"; Flags: nowait postinstall skipifsilent; Components: app
Filename: "{code:OpsisPath}"; Description: "Επανεκκίνηση του Opsis"; Flags: nowait runasoriginaluser; Check: OpsisWasClosed

[Code]
var
  OpsisClosed: Boolean;

function IsRunning(const Image: String): Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\cmd.exe'), '/C tasklist /FI "IMAGENAME eq ' + Image + '" | find /I "' + Image + '" >nul', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

{ Asks the program to close, waits a few seconds, and ends it if it is still there. }
function CloseProgram(const Image: String): Boolean;
var
  Code, I: Integer;
begin
  Result := True;
  if not IsRunning(Image) then Exit;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM "' + Image + '"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  for I := 1 to 16 do
  begin
    if not IsRunning(Image) then Exit;
    Sleep(500);
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM "' + Image + '"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(800);
  Result := not IsRunning(Image);
end;

function OpsisPath(Param: String): String;
begin
  Result := ExpandConstant('{autopf64}\Opsis - Desktop Customization\Opsis.exe');
  if not FileExists(Result) then Result := ExpandConstant('{autopf}\Opsis - Desktop Customization\Opsis.exe');
end;

function OpsisWasClosed: Boolean;
begin
  Result := OpsisClosed and FileExists(OpsisPath(''));
end;

{ Opsis loads the plugins into its own process, so its plugin files (and the ones of the other plugins) are locked while it runs. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CloseProgram('Chronikologio.exe') then
    Result := 'Το Χρονικολόγιο δεν κλείνει. Κλείσε το και ξαναδοκίμασε.';
  if (Result = '') and WizardIsComponentSelected('plugin') then
  begin
    OpsisClosed := IsRunning('Opsis.exe');
    if not CloseProgram('Opsis.exe') then
      Result := 'Το Opsis δεν κλείνει. Κλείσε το (και τα plugins του) και ξαναδοκίμασε.';
  end;
end;
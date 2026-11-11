; Inno Setup script for Chronikologio (also compiles with 7.x). Build with build-installer.bat, which publishes into dist\ first.
;
; Components:
;   app     the stand-alone Chronikologio window (Start menu entry, optional desktop icon)
;   plugin  the Opsis plugin. The setup finds Opsis (its uninstall entry), checks that its plugin contract has the feeds and widgets
;           (OpsisNewsCrawler.Plugin.Abstractions.dll contains "PluginFeeds"), closes Opsis, copies the plugin to <Opsis folder>\plugins\Chronikologio
;           when Opsis is under Program Files (a protected folder: loaded without the SHA-256 approval) or to %AppData%\Opsis\plugins\Chronikologio
;           for a per-user Opsis (Opsis then asks for the approval), and starts Opsis again. Without a suitable Opsis the plugin is not installed and
;           the setup says so. The uninstaller removes only the plugin files and its approval entry, never Opsis. install-plugin.bat is for developers.
; Silent install:  ChronikologioSetup.exe /VERYSILENT /COMPONENTS="app,plugin"
#define MyAppName "Χρονικολόγιο"
#define MyAppVersion "0.9.0"
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
; the pictures of the pages (made from the icon by tools\installer\Make-WizardImages.ps1)
WizardStyle=modern
WizardImageFile=assets\wizard-large.bmp
WizardSmallImageFile=assets\wizard-small.bmp
; The running programs that hold the files are closed by the [Code] section below (Opsis loads the plugins into its own process), without a question.
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

[Languages]
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"; LicenseFile: "license-el.txt"; InfoBeforeFile: "about-el.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "license-en.txt"; InfoBeforeFile: "about-en.txt"

[Types]
Name: "full"; Description: "Πλήρης εγκατάσταση"
Name: "custom"; Description: "Προσαρμοσμένη εγκατάσταση"; Flags: iscustom

[Components]
Name: "app"; Description: "Εφαρμογή Χρονικολόγιο"; Types: full custom; Flags: fixed
Name: "plugin"; Description: "Plugin για το Opsis (ημερολόγιο widget και ροή «Σαν σήμερα»). Απαιτεί Opsis 2.4.3 ή νεότερο· το Opsis κλείνει και ξανανοίγει."; Types: full

[Tasks]
Name: "desktopicon"; Description: "Δημιουργία συντόμευσης στην επιφάνεια εργασίας"; Components: app; Flags: unchecked

[Files]
Source: "{#DistDir}\app\*"; DestDir: "{app}"; Components: app; Flags: recursesubdirs ignoreversion
Source: "license-el.txt"; DestDir: "{app}"; DestName: "LICENSE-el.txt"; Components: app; Flags: ignoreversion
Source: "license-en.txt"; DestDir: "{app}"; DestName: "LICENSE-en.txt"; Components: app; Flags: ignoreversion
; The plugin goes into the plugins folder of the Opsis that was found (see [Code]).
Source: "{#DistDir}\plugin\*"; DestDir: "{code:PluginDir}"; Components: plugin; Flags: recursesubdirs ignoreversion; Check: PluginWillInstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Components: app
Name: "{group}\Απεγκατάσταση"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Components: app

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Εκκίνηση του Χρονικολόγιου"; Flags: nowait postinstall skipifsilent; Components: app
Filename: "{code:OpsisPath}"; Description: "Επανεκκίνηση του Opsis"; Flags: nowait runasoriginaluser; Check: OpsisWasClosed

[Code]
const
  OpsisKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A9E8F765-1234-4567-89AB-CDEF01234567}_is1';

var
  OpsisClosed: Boolean;
  OpsisDir: String;          { folder of the installed Opsis, '' when it is not installed }
  OpsisProtected: Boolean;   { under Program Files: its plugins folder needs no approval }
  OpsisContractOk: Boolean;  { its plugin contract has the widgets and feeds }

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

function ReadOpsisDir: String;
var
  Dir: String;
begin
  Result := '';
  if not RegQueryStringValue(HKLM64, OpsisKey, 'InstallLocation', Dir) then
    if not RegQueryStringValue(HKLM32, OpsisKey, 'InstallLocation', Dir) then
      if not RegQueryStringValue(HKCU64, OpsisKey, 'InstallLocation', Dir) then
        Dir := '';
  Dir := RemoveBackslash(Dir);
  if (Dir <> '') and FileExists(Dir + '\Opsis.exe') then Result := Dir;
end;

function IsUnder(const Dir, Parent: String): Boolean;
begin
  Result := (Dir <> '') and (Parent <> '') and (Pos(Lowercase(AddBackslash(Parent)), Lowercase(AddBackslash(Dir))) = 1);
end;

{ The contract file of Opsis must contain the type PluginFeeds (an older Opsis cannot load a plugin that offers widgets and feeds). }
function ContractHasFeeds(const Dir: String): Boolean;
var
  S: AnsiString;
begin
  Result := LoadStringFromFile(Dir + '\OpsisNewsCrawler.Plugin.Abstractions.dll', S) and (Pos('PluginFeeds', String(S)) > 0);
end;

function OpsisPath(Param: String): String;
begin
  Result := OpsisDir + '\Opsis.exe';
end;

function PluginWillInstall: Boolean;
begin
  Result := (OpsisDir <> '') and OpsisContractOk;
end;

{ <Opsis folder>\plugins\Chronikologio for an Opsis under Program Files, %AppData%\Opsis\plugins\Chronikologio otherwise. }
function PluginDir(Param: String): String;
begin
  if OpsisProtected and (OpsisDir <> '') then Result := OpsisDir + '\plugins\Chronikologio'
  else Result := ExpandConstant('{userappdata}\Opsis\plugins\Chronikologio');
end;

function OpsisWasClosed: Boolean;
begin
  Result := OpsisClosed and (OpsisDir <> '') and FileExists(OpsisPath(''));
end;

procedure FindOpsis;
begin
  OpsisDir := ReadOpsisDir;
  OpsisProtected := IsUnder(OpsisDir, ExpandConstant('{commonpf64}')) or IsUnder(OpsisDir, ExpandConstant('{commonpf32}'));
  OpsisContractOk := (OpsisDir <> '') and ContractHasFeeds(OpsisDir);
  Log('Opsis folder: "' + OpsisDir + '", protected: ' + IntToStr(Ord(OpsisProtected)) + ', contract with feeds: ' + IntToStr(Ord(OpsisContractOk)));
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  FindOpsis;
end;

{ Removes the entry of the plugin from the approvals of Opsis (the SHA-256 of an old build means nothing for the next one). }
procedure ForgetApproval;
var
  Code: Integer;
  Cmd: String;
begin
  Cmd := '-NoProfile -ExecutionPolicy Bypass -Command "$f = Join-Path $env:APPDATA ''Opsis\plugin-trust.json''; if (Test-Path $f) { ' +
    '$all = @(Get-Content $f -Raw | ConvertFrom-Json); $keep = @($all | Where-Object { $_.Name -ne ''Chronikologio'' }); ' +
    'if ($keep.Count -ne $all.Count) { $json = if ($keep.Count -eq 0) { ''[]'' } else { ConvertTo-Json -InputObject $keep }; ' +
    '[IO.File]::WriteAllText($f, $json, (New-Object Text.UTF8Encoding($false))) } }"';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Cmd, '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

{ Opsis loads the plugins into its own process, so its plugin files (and the ones of the other plugins) are locked while it runs. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CloseProgram('Chronikologio.exe') then
    Result := 'Το Χρονικολόγιο δεν κλείνει. Κλείσε το και ξαναδοκίμασε.';
  if WizardIsComponentSelected('plugin') then
  begin
    if OpsisDir = '' then
      SuppressibleMsgBox('Δεν βρέθηκε εγκατεστημένο το Opsis, οπότε το plugin δεν θα εγκατασταθεί. Εγκατέστησε πρώτα το Opsis και τρέξε ξανά αυτό το πρόγραμμα εγκατάστασης.' + #13#10 + 'Opsis was not found, so the plugin is not installed.', mbInformation, MB_OK, IDOK)
    else if not OpsisContractOk then
      SuppressibleMsgBox('Το Opsis που βρέθηκε (' + OpsisDir + ') έχει παλαιότερο συμβόλαιο plugin και δεν μπορεί να φορτώσει το plugin του Χρονικολόγιου, οπότε δεν θα εγκατασταθεί. Αναβάθμισε το Opsis σε έκδοση 2.4.3 ή νεότερη και τρέξε ξανά την εγκατάσταση.' + #13#10 + 'This Opsis has an older plugin contract; the plugin is not installed.', mbInformation, MB_OK, IDOK)
    else if not OpsisProtected then
      SuppressibleMsgBox('Το Opsis είναι εγκατεστημένο σε φάκελο χρήστη: το plugin μπαίνει στο %AppData%\Opsis\plugins και το Opsis θα ζητήσει την έγκρισή σου όταν ξεκινήσει.', mbInformation, MB_OK, IDOK);
  end;
  if (Result = '') and WizardIsComponentSelected('plugin') and PluginWillInstall then
  begin
    OpsisClosed := IsRunning('Opsis.exe');
    if not CloseProgram('Opsis.exe') then
      Result := 'Το Opsis δεν κλείνει. Κλείσε το (και τα plugins του) και ξαναδοκίμασε.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and WizardIsComponentSelected('plugin') and PluginWillInstall then
  begin
    ForgetApproval;
    { the user-level folder of the same plugin would load it twice }
    if OpsisProtected then DelTree(ExpandConstant('{userappdata}\Opsis\plugins\Chronikologio'), True, True, True);
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  OpsisDir := ReadOpsisDir;
end;

{ The plugin files of Opsis are locked while it runs: close it, remove only the plugin (its files, its folder and its approval), start Opsis again. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    OpsisClosed := IsRunning('Opsis.exe');
    if OpsisClosed then CloseProgram('Opsis.exe');
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    if OpsisDir <> '' then DelTree(OpsisDir + '\plugins\Chronikologio', True, True, True);
    DelTree(ExpandConstant('{userappdata}\Opsis\plugins\Chronikologio'), True, True, True);
    ForgetApproval;
    if OpsisClosed and (OpsisDir <> '') and FileExists(OpsisDir + '\Opsis.exe') then
      ExecAsOriginalUser(OpsisDir + '\Opsis.exe', '', OpsisDir, SW_SHOWNORMAL, ewNoWait, Code);
  end;
end;

; Force Sleep installer (Inno Setup 6). Built by build.ps1, which passes the version and,
; when a certificate is configured, the signing command.
;
; Per-user install: no admin rights, installs to %LocalAppData%\Programs\Force Sleep.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
; Never change AppId: winget and Windows use it to recognize upgrades of the same app.
AppId={{1813EBC0-EB16-4920-9A13-150CFCE3E135}
AppName=Force Sleep
AppVersion={#AppVersion}
AppVerName=Force Sleep {#AppVersion}
AppPublisher=Niyazi Berkay Çokuysal
AppPublisherURL=https://github.com/niyazicokuysal/windows-force-sleep
AppSupportURL=https://github.com/niyazicokuysal/windows-force-sleep/issues
VersionInfoVersion={#AppVersion}
PrivilegesRequired=lowest
DefaultDirName={autopf}\Force Sleep
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
OutputDir=..\dist
OutputBaseFilename=ForceSleep-Setup-{#AppVersion}
SetupIconFile=..\assets\ForceSleep.ico
UninstallDisplayIcon={app}\ForceSleep.exe
UninstallDisplayName=Force Sleep
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Always ask: plenty of people use Windows in a language other than their own.
ShowLanguageDialog=yes
; We close the running app ourselves in PrepareToInstall; this is the fallback.
CloseApplications=yes
RestartApplications=no
#ifdef Sign
SignTool=forcesleep
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.AutoStart=Start Force Sleep when I sign in to Windows
turkish.AutoStart=Windows'a giriş yapınca Force Sleep'i başlat
english.LaunchApp=Start Force Sleep now
turkish.LaunchApp=Force Sleep'i şimdi başlat

[Tasks]
Name: "autostart"; Description: "{cm:AutoStart}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\ForceSleep.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Force Sleep"; Filename: "{app}\ForceSleep.exe"
Name: "{autodesktop}\Force Sleep"; Filename: "{app}\ForceSleep.exe"; Tasks: desktopicon

[Registry]
; Must match AutoStart.Command in the app exactly, so its menu shows the right check state.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ForceSleep"; ValueData: """{app}\ForceSleep.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\ForceSleep.exe"; Parameters: "--default-language {code:LanguageCode}"; Flags: runhidden waituntilterminated; Check: PickedOtherLanguageThanWindows
Filename: "{app}\ForceSleep.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Closes the app and removes its start-with-Windows entry, which the app may have written itself.
Filename: "{app}\ForceSleep.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "ForceSleepUninstall"

[Code]
const
  LangTurkish = $1F;

function LanguageCode(Param: String): String;
begin
  if ActiveLanguage = 'turkish' then
    Result := 'tr'
  else
    Result := 'en';
end;

{ Only pin the app's language when it would otherwise differ, e.g. Turkish picked on English Windows,
  so by default the app keeps following the Windows language. }
function PickedOtherLanguageThanWindows: Boolean;
begin
  Result := (ActiveLanguage = 'turkish') <> ((GetUILanguage and $3FF) = LangTurkish);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Exe: String;
  ResultCode: Integer;
begin
  { Close a running copy so its file can be replaced. }
  Exe := ExpandConstant('{app}\ForceSleep.exe');
  if FileExists(Exe) then
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

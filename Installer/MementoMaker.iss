#define MyAppName "Memento Maker"
#define MyAppVersion "0.9.91"
#define MyAppDisplayVersion "0.9.911 Beta"
#define MyAppExeName "MementoMaker.exe"
#define MyAppPublisher "SeanB - Developed with assistance from ChatGPT by OpenAI"

[Setup]
AppId={{A78B070D-A7DF-4E56-98A5-92A79E3C7435}
AppName={#MyAppName}
AppVersion={#MyAppDisplayVersion}
AppVerName={#MyAppName} {#MyAppDisplayVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Memento Maker
DefaultGroupName=Memento Maker
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=MementoMakerSetup_0.9.91_Beta
SetupIconFile=..\Theme\MM_Icon.ico
UninstallDisplayIcon={app}\MementoMaker.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=140,140
WizardImageFile=Assets\WizardLarge.png
WizardSmallImageFile=Assets\WizardSmall.png
WizardImageStretch=yes
WizardImageBackColor=#fff7e2
WizardSmallImageBackColor=#fff7e2
DisableWelcomePage=no
InfoBeforeFile=BETA_NOTICE.txt
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
VersionInfoVersion=0.9.911.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Memento Maker Setup
VersionInfoProductName=Memento Maker
VersionInfoProductVersion=0.9.911.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Memento Maker"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Memento Maker"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Keep post-install launch unelevated. Explorer normally runs unelevated and Windows blocks
; drag/drop across integrity levels when Memento Maker inherits Setup's administrator token.
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Memento Maker"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
const
  DotNet48Release = 528040;
  CreamColor = $00D7F4FF;
  LightCreamColor = $00EBF9FF;
  NavyColor = $00523300;
  GoldColor = $0011B2F5;

var
  PersistentSetupLogFile: String;
  PersistentTechnicalSetupLogFile: String;
  PersistentUninstallLogFile: String;

function InstallerLogFolder(): String;
begin
  Result := ExpandConstant('{localappdata}\MementoMaker\Logs\Installer');
end;

function NewPersistentLogPath(const Prefix: String): String;
var
  Folder: String;
  Stamp: String;
begin
  Folder := InstallerLogFolder();
  ForceDirectories(Folder);
  Stamp := GetDateTimeString('yyyy-mm-dd_hhnnss', #0, #0);
  Result := AddBackslash(Folder) + Prefix + '_' + Stamp + '.log';
end;

procedure AppendPersistentLog(var LogFile: String; const MessageText: String; const Prefix: String);
var
  Lines: TArrayOfString;
begin
  if LogFile = '' then
    LogFile := NewPersistentLogPath(Prefix);

  SetArrayLength(Lines, 1);
  Lines[0] := '[' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', #0, ':') + '] ' + MessageText;
  try
    if not SaveStringsToUTF8File(LogFile, Lines, True) then
      Log('Persistent installer log write failed: ' + LogFile);
  except
    Log('Persistent installer log write raised an exception; setup will continue.');
  end;
end;

procedure AppendSetupLog(const MessageText: String);
begin
  AppendPersistentLog(PersistentSetupLogFile, MessageText, 'Install');
end;

procedure AppendUninstallLog(const MessageText: String);
begin
  AppendPersistentLog(PersistentUninstallLogFile, MessageText, 'Uninstall');
end;

procedure EnsureTechnicalSetupLogPath();
begin
  if PersistentTechnicalSetupLogFile = '' then
    PersistentTechnicalSetupLogFile := NewPersistentLogPath('Install_Technical');
end;

procedure CopyTechnicalSetupLogSnapshot(const Stage: String);
var
  SourceLog: String;
begin
  try
    EnsureTechnicalSetupLogPath();
    SourceLog := ExpandConstant('{log}');
    if (SourceLog = '') or (not FileExists(SourceLog)) then
    begin
      AppendSetupLog('Technical Inno Setup log is not available yet at checkpoint: ' + Stage + '.');
      Exit;
    end;

    if CopyFile(SourceLog, PersistentTechnicalSetupLogFile, False) then
      Log('Persistent technical installer log snapshot saved at checkpoint: ' + Stage)
    else
      Log('Persistent technical installer log snapshot could not be copied at checkpoint: ' + Stage);
  except
    Log('Persistent technical installer log snapshot raised an exception at checkpoint: ' + Stage);
  end;
end;

function IsDotNet48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if IsWin64 then
    Result := RegQueryDWordValue(HKLM64,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release', Release) and (Release >= DotNet48Release);

  if not Result then
    Result := RegQueryDWordValue(HKLM32,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release', Release) and (Release >= DotNet48Release);
end;

function InitializeSetup(): Boolean;
begin
  { Create both persistent paths as early as possible so an intermittent Setup/SpawnServer
    failure still leaves diagnostic files under the normal Memento Maker log folder. }
  if PersistentSetupLogFile = '' then
    PersistentSetupLogFile := NewPersistentLogPath('Install');
  EnsureTechnicalSetupLogPath();

  AppendSetupLog('Memento Maker {#MyAppDisplayVersion} setup started.');
  AppendSetupLog('Setup executable: ' + ExpandConstant('{srcexe}'));
  AppendSetupLog('Native Inno Setup logging enabled. Technical snapshot: ' + PersistentTechnicalSetupLogFile);
  CopyTechnicalSetupLogSnapshot('InitializeSetup - before prerequisite check');

  Result := IsDotNet48Installed();
  if Result then
  begin
    AppendSetupLog('.NET Framework 4.8 prerequisite check passed.');
    CopyTechnicalSetupLogSnapshot('InitializeSetup - prerequisite check passed');
  end
  else
  begin
    AppendSetupLog('.NET Framework 4.8 prerequisite check failed; setup aborted.');
    CopyTechnicalSetupLogSnapshot('InitializeSetup - prerequisite check failed');
    MsgBox(
      'Memento Maker requires Microsoft .NET Framework 4.8.' + #13#10 + #13#10 +
      'Install .NET Framework 4.8, then run Memento Maker Setup again.',
      mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    AppendSetupLog('Installation started. Target folder: ' + ExpandConstant('{app}'));
    CopyTechnicalSetupLogSnapshot('ssInstall');
  end
  else if CurStep = ssPostInstall then
  begin
    AppendSetupLog('Application files and shortcuts installed.');
    CopyTechnicalSetupLogSnapshot('ssPostInstall');
  end
  else if CurStep = ssDone then
  begin
    AppendSetupLog('Installation completed successfully.');
    CopyTechnicalSetupLogSnapshot('ssDone');
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  AppendSetupLog('Wizard page displayed. Page ID: ' + IntToStr(CurPageID));
  CopyTechnicalSetupLogSnapshot('Wizard page ' + IntToStr(CurPageID));
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  AppendSetupLog('Next requested from wizard page ID: ' + IntToStr(CurPageID));
  CopyTechnicalSetupLogSnapshot('Before Next from wizard page ' + IntToStr(CurPageID));
  Result := True;
end;

procedure DeinitializeSetup();
begin
  AppendSetupLog('Setup process ended.');
  CopyTechnicalSetupLogSnapshot('DeinitializeSetup');
end;

function InitializeUninstall(): Boolean;
begin
  AppendUninstallLog('Memento Maker uninstall started.');
  AppendUninstallLog('Installed application folder: ' + ExpandConstant('{app}'));
  AppendUninstallLog('User data under %LOCALAPPDATA%\MementoMaker is preserved.');
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    AppendUninstallLog('Removing installed application files and shortcuts.')
  else if CurUninstallStep = usPostUninstall then
    AppendUninstallLog('Application removal complete; preserved user data was not deleted.')
  else if CurUninstallStep = usDone then
    AppendUninstallLog('Uninstall completed successfully.');
end;

procedure DeinitializeUninstall();
begin
  AppendUninstallLog('Uninstall process ended.');
end;

procedure ApplyMementoTheme();
begin
  WizardForm.Color := CreamColor;
  WizardForm.MainPanel.Color := CreamColor;
  WizardForm.InnerPage.Color := LightCreamColor;
  WizardForm.WelcomePage.Color := LightCreamColor;
  WizardForm.FinishedPage.Color := LightCreamColor;

  WizardForm.PageNameLabel.Font.Color := NavyColor;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := NavyColor;
  WizardForm.WelcomeLabel1.Font.Color := NavyColor;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Font.Color := NavyColor;
  WizardForm.FinishedHeadingLabel.Font.Color := NavyColor;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Font.Color := NavyColor;

  WizardForm.WelcomeLabel1.Caption := 'Welcome to Memento Maker';
  WizardForm.WelcomeLabel2.Caption :=
    'Create, build and share custom item mods for Two Point Museum.' + #13#10 + #13#10 +
    'Setup will install Memento Maker on this PC. Unity and the official Modding SDK remain separate and are located by Memento Maker during first-run setup.';
  WizardForm.FinishedHeadingLabel.Caption := 'Memento Maker is ready';
  WizardForm.FinishedLabel.Caption :=
    'Memento Maker has been installed successfully.' + #13#10 + #13#10 +
    'Launch the app to complete first-run setup and verify Unity, the official Modding SDK and Steam Workshop connectivity.' + #13#10 + #13#10 +
    'Special Thanks' + #13#10 +
    'Beta Testers: Kiwiphant & Linelle Kardeen';
end;

procedure InitializeWizard();
begin
  ApplyMementoTheme();
  AppendSetupLog('Setup wizard initialized.');
  CopyTechnicalSetupLogSnapshot('InitializeWizard');
end;

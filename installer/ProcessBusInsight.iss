; Process Bus Insight – stable, offline, per-machine Inno Setup 6 installer.
; Build by scripts/publish-windows-installer.ps1 after staging a verified portable package.
; This script does NOT redistribute or attempt to install Npcap.

#ifndef AppVersion
  #error AppVersion is required (/DAppVersion=...)
#endif
#ifndef FileVersion
  #error FileVersion is required (/DFileVersion=...)
#endif
#ifndef StageDir
  #error StageDir is required (/DStageDir=...)
#endif
#ifndef OutputDir
  #error OutputDir is required (/DOutputDir=...)
#endif

#define AppName "Process Bus Insight"
#define AppExe "ProcessBusInsight.exe"
#define RepoUrl "https://github.com/masarray/DigSubAnalyzer"

[Setup]
; Stable AppId: never change between releases or upgrades will create duplicates.
AppId={{EAF21D82-164A-4EEA-9DAE-D4358769D10A}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Process Bus Insight Contributors
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases/latest
DefaultDirName={autopf}\Process Bus Insight
DefaultGroupName=Process Bus Insight
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=ProcessBusInsight-v{#AppVersion}-win-x64-setup
SetupIconFile={#SourcePath}\..\src\ProcessBus.App.Wpf\IEC61850.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
ChangesAssociations=no
DisableWelcomePage=no
MinVersion=10.0
LicenseFile={#StageDir}\LICENSE.txt
InfoAfterFile={#StageDir}\README.txt
VersionInfoDescription=Process Bus Insight Setup
VersionInfoProductName=Process Bus Insight
VersionInfoCompany=Process Bus Insight Contributors
VersionInfoVersion={#FileVersion}
; Installer remains unsigned until a legitimate code-signing certificate is configured.
; Never imply that an unsigned build bypasses Windows SmartScreen.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Exact files verified by the release-packaging gate. No customer configuration or
; capture data is copied into Program Files by this installer.
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Process Bus Insight"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autoprograms}\User Manual (PDF)"; Filename: "{app}\User Manual.pdf"
Name: "{autoprograms}\Quick Start (PDF)"; Filename: "{app}\Quick Start.pdf"
Name: "{autoprograms}\Uninstall Process Bus Insight"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Process Bus Insight"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch Process Bus Insight"; Flags: nowait postinstall skipifsilent

[Code]
function IsNpcapPresent(): Boolean;
begin
  // Npcap has both native 64-bit and compatibility DLL deployments. Only give
  // guidance, since offline PCAP replay works without a live capture driver.
  Result :=
    FileExists(ExpandConstant('{sys}\Npcap\wpcap.dll')) or
    FileExists(ExpandConstant('{sys}\wpcap.dll'));
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpReady) and (not IsNpcapPresent()) then
    WizardForm.ReadyMemo.Lines.Add(
      'Note: Npcap was not detected. Live Ethernet capture requires Npcap, ' +
      'installed separately. Offline analysis can still be used.');
end;

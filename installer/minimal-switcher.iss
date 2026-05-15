#define MyAppName "Minimal Switcher"
#define MyAppPublisher "wv357"
#define MyAppExeName "minimal-switcher.exe"
#define MyTaskName "MinimalSwitcher"
#define MyAppId "{{7F96AF29-1F66-4B6B-A9A8-5C82C7B445E8}"
#define MyAppVersion GetEnv("APP_VERSION")
#if MyAppVersion == ""
  #define MyAppVersion "0.1.0"
#endif
#define SourceDir GetEnv("PUBLISH_DIR")
#if SourceDir == ""
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#define OutputDir GetEnv("INSTALLER_OUTPUT_DIR")
#if OutputDir == ""
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/wv357/minimal-switcher
AppSupportURL=https://github.com/wv357/minimal-switcher/issues
AppUpdatesURL=https://github.com/wv357/minimal-switcher/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=MinimalSwitcherSetup-{#MyAppVersion}
SetupIconFile=..\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "startup"; Description: "Iniciar Minimal Switcher con Windows con privilegios elevados"; GroupDescription: "Inicio:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{sys}\schtasks.exe"; Parameters: "/Create /TN ""{#MyTaskName}"" /TR ""\""{app}\{#MyAppExeName}\"""" /SC ONLOGON /RL HIGHEST /F"; Flags: runhidden; Tasks: startup
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#MyTaskName}"" /F"; Flags: runhidden; Tasks: not startup
Filename: "{sys}\schtasks.exe"; Parameters: "/Run /TN ""{#MyTaskName}"""; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent runhidden; Tasks: startup
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent; Tasks: not startup

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#MyTaskName}"" /F"; Flags: runhidden

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

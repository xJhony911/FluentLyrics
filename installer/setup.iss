; Inno Setup Script for FluentLyrics
#define MyAppName "FluentLyrics"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Antigravity"
#define MyAppURL "https://github.com/xJhony911/FluentLyrics"
#define MyAppExeName "FluentLyrics.exe"

[Setup]
AppId={{E612B7D4-7389-4B52-9457-4180A597E3B1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer_output
OutputBaseFilename=FluentLyrics-Setup-v1.2.0
SetupIconFile=..\Assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Iniciar FluentLyrics automáticamente con Windows"; GroupDescription: "Inicio del sistema:"; Flags: checkedonce

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\icon.ico"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

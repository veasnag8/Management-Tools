; Script generated for Inno Setup 6
; Tool Dubber Windows Installer v2.0.6

#define MyAppName "Tool Dubber"
#define MyAppVersion "2.0.6"
#define MyAppPublisher "SNA Studio"
#define MyAppExeName "SnaDubber.exe"
#define MyAppSourceDir "D:\Project\Release_v1.2.0\SNA-Dubber"
#define MyOutputDir "D:\Project\Release_v1.2.0\Output"

[Setup]
AppId={{8B1B6CE7-3FD4-411A-B472-E40B7EE84920}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
OutputDir={#MyOutputDir}
OutputBaseFilename=ToolDubber-Setup-v{#MyAppVersion}
SetupIconFile={#MyAppSourceDir}\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "hook.log,*.pdb,*.tmp,FilledCache\*,FlippedCache\*,PreviewCache\*"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Setup]
AppId={{7F38A82D-C174-4C2E-8EBE-B5817E1A9D42}
AppName=SNA Pro
AppVersion=1.0.0
AppVerName=SNA Pro 1.0.0
AppPublisher=SNA Pro
DefaultDirName={autopf}\SNA Pro
DefaultGroupName=SNA Pro
AllowNoIcons=yes
OutputDir=D:\Python\Download\license-platform\windows-client\Output
OutputBaseFilename=SNA_Pro_v1.0.0_Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "D:\Python\Download\license-platform\windows-client\bin\ReleaseBundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SNA Pro"; Filename: "{app}\Tool.exe"
Name: "{group}\{cm:UninstallProgram,SNA Pro}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SNA Pro"; Filename: "{app}\Tool.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Tool.exe"; Description: "{cm:LaunchProgram,SNA Pro}"; Flags: nowait postinstall skipifsilent

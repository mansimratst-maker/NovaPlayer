#define AppName "NovaPlayer"
#define AppVersion "1.0.3"
#define SourceDir "..\..\dist\NovaPlayer-Windows-x64\NovaPlayer"

[Setup]
AppId={{74C9C7BE-3F30-45A9-A75E-489BD889B607}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=NovaPlayer
AppPublisherURL=https://github.com/mansimratst-maker/NovaPlayer
DefaultDirName={localappdata}\Programs\NovaPlayer
DefaultGroupName=NovaPlayer
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\dist
OutputBaseFilename=NovaPlayer-Setup-{#AppVersion}-x64
SetupIconFile=..\..\NovaPlayer\Assets\novaplayer.ico
UninstallDisplayIcon={app}\NovaPlayer.exe
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\NovaPlayer"; Filename: "{app}\NovaPlayer.exe"
Name: "{autodesktop}\NovaPlayer"; Filename: "{app}\NovaPlayer.exe"; Tasks: desktopicon
Name: "{group}\Uninstall NovaPlayer"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\NovaPlayer.exe"; Description: "Launch NovaPlayer"; Flags: postinstall nowait skipifsilent

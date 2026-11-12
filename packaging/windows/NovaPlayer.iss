#define AppName "Nivora Player"
#define AppVersion "1.1.0"
#define SourceDir "..\..\dist\NivoraPlayer-Windows-x64\NivoraPlayer"

[Setup]
AppId={{74C9C7BE-3F30-45A9-A75E-489BD889B607}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Nivora Player
AppPublisherURL=https://github.com/mansimratst-maker/NovaPlayer
DefaultDirName={localappdata}\Programs\NovaPlayer
DefaultGroupName=Nivora Player
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\dist
OutputBaseFilename=NivoraPlayer-Setup-{#AppVersion}-x64
SetupIconFile=..\..\NovaPlayer\Assets\novaplayer.ico
UninstallDisplayIcon={app}\NivoraPlayer.exe
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
Name: "{group}\Nivora Player"; Filename: "{app}\NivoraPlayer.exe"
Name: "{autodesktop}\Nivora Player"; Filename: "{app}\NivoraPlayer.exe"; Tasks: desktopicon
Name: "{group}\Uninstall Nivora Player"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\NivoraPlayer.exe"; Description: "Launch Nivora Player"; Flags: postinstall nowait skipifsilent

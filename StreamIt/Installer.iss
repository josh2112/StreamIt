; -- StreamIt Install Script --

#define Application   "StreamIt"

#define Exe           "StreamIt.exe"
#define ExeDir        "bin\Release\net8.0-windows8.0"
#define ExeVersion    GetFileVersion( AddBackslash( ExeDir ) + Exe )

#define Company       "Josh2112 Apps"

[Setup]
AppName={#Application}
AppVerName={#Application}
AppVersion={#ExeVersion}
AppPublisher={#Company}
AppPublisherURL=https://www.josh2112.com/apps/
AppCopyright=Copyright (C) 2024 {#Company}
VersionInfoVersion={#ExeVersion}
DefaultDirName={commonpf}\{#Company}\{#Application}
DefaultGroupName={#Company}\{#Application}
Compression=lzma2
SolidCompression=yes
OutputDir=..\Installers
OutputBaseFilename="{#Application} Installer - {#ExeVersion}"
DirExistsWarning=no
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon="{app}\{#Exe}"

[Files]
Source: "{#ExeDir}\*"; Excludes: "*.pdb,*.xml,*.vshost.*"; DestDir: "{app}"; Flags: replacesameversion recursesubdirs

[Icons]
Name: "{group}\{#Application}"; Filename: "{app}\{#Exe}"
Name: "{commondesktop}\{#Application}"; Filename: "{app}\{#Exe}"

[Run]
Filename: "{app}\{#Exe}"; Description: "Run {#Application}"; Flags: postinstall skipifsilent nowait

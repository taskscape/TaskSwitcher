#define MyAppName "TaskSwitcher"
#define MyAppPublisher "Taskscape Ltd"
#define MyAppURL "https://www.taskscape.com"
#define MyAppExeName "TaskSwitcher.exe"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#ifndef MyAppPath
#define MyAppPath "..\TaskSwitcher\bin\Release\net10.0-windows7.0\win-x64\publish"
#endif

[Setup]
; App Information
AppId={{A5AF4C34-70A7-4D3B-BA18-E49C0AEEA5E6}
AppMutex=DBDE24E4-91F6-11DF-B495-C536DFD72085-TaskSwitcher
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation Settings
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
ShowLanguageDialog=auto

; Output Settings
OutputDir=Output
OutputBaseFilename=TaskSwitcher-Setup-{#MyAppVersion}
SetupIconFile=..\TaskSwitcher\icon.ico

; Compression
Compression=lzma2
SolidCompression=yes

; Architecture Settings (64-bit only)
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

; Privileges
PrivilegesRequired=admin

; Uninstall Settings
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
Type: files; Name: "{commonstartup}\{#MyAppName}.lnk"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupfolder"; Description: "Start with Windows"; GroupDescription: "Additional tasks:"

[Files]
Source: "{#MyAppPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupfolder

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent


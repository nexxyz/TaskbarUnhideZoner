#define AppName "Taskbar Unhide Zoner"
#define AppVersion "0.9.2"
#define AppExeName "TaskbarUnhideZoner.exe"
#define ArchFlag "x64compatible"

[Setup]
AppId={{A8AF7F8B-1A77-4C75-8B58-5DF6A9234022}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\TaskbarUnhideZoner
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=TaskbarUnhideZoner-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode={#ArchFlag}
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\assets\TaskbarUnhideZoner.ico

[Tasks]
Name: "startup"; Description: "Start {#AppName} with Windows"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "..\src\TaskbarUnhideZoner\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{autoprograms}\{#AppName} (Uninstall)"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

#define AppName    "Меню питания семьи"
#define AppVersion "2.3.2"
#define AppExeName "MenuApp.exe"
#define AppSourceDir "..\..\Дистрибутив v2.3.2\app"
#define OutputDir  "..\..\setup_output"

[Setup]
AppId={{CCD8B97A-154C-4E28-994A-1FA6024EB421}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=andrey1b
DefaultDirName={localappdata}\MenuApp
DefaultGroupName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=MenuApp_Setup_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";                       Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";                 Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

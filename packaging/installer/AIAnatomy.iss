; Inno Setup script for the AI Anatomy installer (AIAnatomy-Setup.exe).
; Built by .github/workflows/release.yml:  iscc /DAppVersion=1.2.3 /DPublishDir=<publish folder> /DOutputDir=<out> AIAnatomy.iss
; Installs to C:\Program Files\Qapow\AIAnatomy by default (every QAPOW program goes under C:\Program Files\Qapow),
; and the user can pick another folder. The app keeps progress and settings in %LocalAppData%\AIAnatomy, not here.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\out"
#endif

#define AppName "AI Anatomy"
#define AppFolder "AIAnatomy"
#define AppExe "AIAnatomy.App.exe"

[Setup]
; AppId identifies this program to Windows across versions. Never change it.
AppId={{94F22115-F5DB-4628-8BE8-C89394028CEF}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=John Apolloni
AppPublisherURL=https://qapow.com/
AppSupportURL=https://github.com/johnovanni/AIAnatomy
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\Qapow\{#AppFolder}
DisableDirPage=no
DefaultGroupName=Qapow
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\..\src\AIAnatomy.App\AIAnatomy.ico
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
OutputDir={#OutputDir}
OutputBaseFilename=AIAnatomy-Setup

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Qapow\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

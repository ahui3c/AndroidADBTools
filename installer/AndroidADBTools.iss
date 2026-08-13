#ifndef MyAppVersion
  #define MyAppVersion "2.0.6"
#endif
#ifndef PackageSource
  #define PackageSource "..\release\AndroidADBTools-v" + MyAppVersion + "-complete"
#endif
#ifndef ReleaseOutput
  #define ReleaseOutput "..\release"
#endif
#ifndef InstallerPrivileges
  #define InstallerPrivileges "admin"
#endif

#define MyAppName "Android ADB 快速工具"
#define MyAppExeName "AndroidADBTools.exe"
#define MyAppPublisher "廖阿輝"
#define MyAppUrl "https://github.com/ahui3c/AndroidADBTools"

[Setup]
AppId={{4C83DBD5-2F92-4B44-97A5-71D18B18AF72}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 完整安裝程式
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={autopf}\AndroidADBTools
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
OutputDir={#ReleaseOutput}
OutputBaseFilename=AndroidADBTools-v{#MyAppVersion}-complete-setup
SetupIconFile=..\assets\app-icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired={#InstallerPrivileges}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; GroupDescription: "其他工作："; Flags: unchecked

[Files]
Source: "{#PackageSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{app}\APKs"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\解除安裝 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "啟動 {#MyAppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and
    (Release >= 528040);
  if not Result then
    Result := RegQueryDWordValue(HKLM32,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and
      (Release >= 528040);
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNet48Installed;
  if not Result then
    MsgBox('Android ADB 快速工具需要 Microsoft .NET Framework 4.8。請先完成安裝後再執行此安裝程式。',
      mbError, MB_OK);
end;

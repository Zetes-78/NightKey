; NightKey installer - Inno Setup 6.3 or newer (https://jrsoftware.org/isinfo.php)
; Build with ..\build.ps1 rather than compiling this directly; it publishes the app first
; and passes the version from NightKey.csproj.

#define AppName "NightKey"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define Publisher "Night Tech Solutions"
#define PublisherUrl "https://nighttech.solutions/"
#define ExeName "NightKey.exe"

[Setup]
; Never change AppId after the first public release: Windows uses it to match upgrades.
AppId={{CF7FE50B-B817-49B2-8CCC-3A93F1F5371B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL={#PublisherUrl}
AppSupportURL={#PublisherUrl}
AppUpdatesURL={#PublisherUrl}
AppCopyright=© 2026 {#Publisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#Publisher}
VersionInfoDescription={#AppName} installer

; Per-user install into %LocalAppData%\Programs\NightKey: no admin prompt.
; IT staff can still choose "Install for all users" from the first screen.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

; Windows 10 version 2004 or newer, 64-bit
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Asks people to close NightKey before upgrading or uninstalling (matches App.MutexName)
AppMutex=Local\NightTechSolutions.NightKey
CloseApplications=yes

LicenseFile=..\LICENSE.txt
SetupIconFile=..\src\NightKey\Assets\nightkey.ico
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=branding\wizard-100.bmp,branding\wizard-200.bmp
WizardSmallImageFile=branding\small-100.bmp,branding\small-200.bmp

OutputDir=..\dist
OutputBaseFilename=NightKey-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes

#ifdef Sign
SignTool=nksign
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This installs [name/ver] on your computer.%n%nNightKey shows your two-step sign-in codes right on your PC. Your secret keys are encrypted on this computer and never leave it.%n%nClose other apps before you continue.
FinishedLabel=NightKey is installed. To add your first account, open the site's two-step sign-in setup so its QR code is showing, then choose Add account in NightKey.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\{#ExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#ExeName}"; Comment: "Two-step sign-in codes"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Uninstalling never deletes saved accounts silently: losing them can lock someone out of every site.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\Night Tech Solutions\NightKey');
    if DirExists(DataDir) and not UninstallSilent then
    begin
      if MsgBox('Also delete your saved NightKey accounts from this PC?' + #13#10 + #13#10 +
                'Choose No to keep them, so they come back if you reinstall NightKey. ' +
                'Only choose Yes if you have a NightKey backup or no longer need these codes.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;

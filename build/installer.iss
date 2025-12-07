; Inno Setup Script for VBDLIS Tools
; Requires: Inno Setup 6.0 or later
; Download from: https://jrsoftware.org/isinfo.php

#define MyAppName "VBDLIS Tools"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Haihv"
#define MyAppURL "https://github.com/haitnmt/Vbdlis-Tools"
#define MyAppExeName "Haihv.Vbdlis.Tools.Desktop.exe"
#define MyAppId "{{8F9A5D3C-1E2B-4C7D-9A8E-6F5B4D3C2E1A}"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=VbdlisTools-Setup-v{#MyAppVersion}
SetupIconFile=..\src\Haihv.Vbdlis.Tools\Haihv.Vbdlis.Tools.Desktop\Assets\avalonia-logo.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

; Version info
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright (C) 2024 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create uninstall registry keys for auto-update
    RegWriteStringValue(HKEY_CURRENT_USER, 'Software\{#MyAppName}', 'InstallPath', ExpandConstant('{app}'));
    RegWriteStringValue(HKEY_CURRENT_USER, 'Software\{#MyAppName}', 'Version', '{#MyAppVersion}');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Clean up registry
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\{#MyAppName}');

    // Optional: Clean up Playwright browsers cache
    // DelTree(ExpandConstant('{localappdata}\ms-playwright'), True, True, True);
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

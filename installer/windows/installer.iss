; Ten Second Tom - Windows Installer Script
; Built with Inno Setup 6.x
;
; This script creates a Windows installer that:
; - Installs all files to Program Files
; - Adds the install directory to user PATH
; - Creates an uninstaller
; - Registers with Windows Apps & Features

#define MyAppName "Ten Second Tom"
#define MyAppExeName "tom.exe"
#define MyAppPublisher "SirKirby"
#define MyAppURL "https://github.com/sirkirby/ten-second-tom"

; Version is passed from command line: /DMyAppVersion=0.9.0
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

; Source directory - relative to this .iss file (installer/windows/)
#define SourceDir "..\..\publish"

[Setup]
; Unique identifier for this application (do not change after first release)
AppId={{8F3A5E2D-7C4B-4E8F-9A1D-6B2C3D4E5F60}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\TenSecondTom
DefaultGroupName={#MyAppName}
; No Start Menu group needed for CLI tool
DisableProgramGroupPage=yes
; MIT License
LicenseFile=..\..\LICENSE
; Output settings (overridden by command line)
OutputDir=..\..\artifacts\installer
OutputBaseFilename=ten-second-tom-{#MyAppVersion}-win-x64-setup
; Compression
Compression=lzma2
SolidCompression=yes
; Modern Windows styling
WizardStyle=modern
; Require Windows 10 or later
MinVersion=10.0
; 64-bit only
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Uninstall settings
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; Elevation not required - install to user profile if no admin
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Main executable
Source: "{#SourceDir}\tom.exe"; DestDir: "{app}"; Flags: ignoreversion

; Configuration files
Source: "{#SourceDir}\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\appsettings.Development.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Native DLLs (ONNX Runtime, AI Foundry, etc.)
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; Whisper.NET runtime libraries (Windows only - exclude Linux .so files)
Source: "{#SourceDir}\runtimes\cuda\win-x64\*"; DestDir: "{app}\runtimes\cuda\win-x64"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Registry]
; Add to user PATH (non-admin install)
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Tasks: addtopath; Check: NeedsAddPath(ExpandConstant('{app}'))

[Tasks]
Name: "addtopath"; Description: "Add to PATH (recommended for CLI usage)"; GroupDescription: "Additional options:"; Flags: checkedonce

[Icons]
; Optional: Create Start Menu shortcut (hidden by default for CLI tools)
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "shell"; Comment: "Start Ten Second Tom interactive shell"

[Run]
; Show version after install to verify it works
Filename: "{app}\{#MyAppExeName}"; Parameters: "--version"; Flags: nowait postinstall skipifsilent runhidden

[Code]
// Check if path needs to be added (avoid duplicates)
function NeedsAddPath(Param: string): Boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  // Look for the path with leading and trailing semicolons
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

// Remove from PATH on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Path: string;
  AppPath: string;
  P: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Path) then
    begin
      AppPath := ExpandConstant('{app}');
      P := Pos(';' + AppPath, Path);
      if P > 0 then
      begin
        Delete(Path, P, Length(';' + AppPath));
        RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Path);
      end
      else
      begin
        P := Pos(AppPath + ';', Path);
        if P > 0 then
        begin
          Delete(Path, P, Length(AppPath + ';'));
          RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Path);
        end
        else
        begin
          P := Pos(AppPath, Path);
          if P > 0 then
          begin
            Delete(Path, P, Length(AppPath));
            RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Path);
          end;
        end;
      end;
    end;
  end;
end;

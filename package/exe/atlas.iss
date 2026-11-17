#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef PayloadDir
  #define PayloadDir "..\..\bin-win"
#endif

#define AppName "Atlas"
#define AppExeName "atlas.exe"

#if FileExists(AddBackslash(SourcePath) + "atlas.ico")
  #define ShortcutIcon "{app}\atlas.ico"
  #define HasIcon
#else
  #define ShortcutIcon "{app}\atlas.exe"
#endif

[Setup]
AppId={{7C4B1D2E-9A3F-4E68-B5C7-1F0A2D9E6B34}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Atlas
DefaultDirName={localappdata}\Programs\Atlas
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\..\dist
OutputBaseFilename=atlas-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
#ifdef HasIcon
SetupIconFile=atlas.ico
#endif

[Tasks]
Name: "autostart"; Description: "登录 Windows 时自动启动 Atlas"; GroupDescription: "附加任务:"; Flags: checkedonce

[Registry]
Root: HKCU; Subkey: "Software\Atlas"; ValueType: string; ValueName: "Port"; ValueData: "{code:GetPort}"; Flags: uninsdeletekey

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.dbg"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "atlas-launcher.vbs"; DestDir: "{app}"; Flags: ignoreversion
Source: "atlas.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{userprograms}\{#AppName}\打开 Atlas"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\atlas-launcher.vbs"" open"; IconFilename: "{#ShortcutIcon}"
Name: "{userprograms}\{#AppName}\启动 Atlas"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\atlas-launcher.vbs"""; IconFilename: "{#ShortcutIcon}"
Name: "{userdesktop}\{#AppName}"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\atlas-launcher.vbs"" open"; IconFilename: "{#ShortcutIcon}"
Name: "{userstartup}\{#AppName}"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\atlas-launcher.vbs"""; IconFilename: "{#ShortcutIcon}"; Tasks: autostart

[Run]
Filename: "{sys}\wscript.exe"; Parameters: """{app}\atlas-launcher.vbs"" open"; Flags: runhidden nowait postinstall skipifsilent; Description: "启动 Atlas 并打开页面"

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExeName} /F"; Flags: runhidden; RunOnceId: "StopAtlas"

[Code]
var
  PortPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  PortPage := CreateInputQueryPage(wpSelectDir,
    '服务端口', '设置 Atlas 监听的端口',
    '默认端口 5000。如果该端口已被其它程序占用，可以在这里修改。');
  PortPage.Add('端口:', False);
  PortPage.Values[0] := ExpandConstant('{param:PORT|5000}');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Port: Integer;
begin
  Result := True;
  if CurPageID = PortPage.ID then
  begin
    if Trim(PortPage.Values[0]) = '' then
      PortPage.Values[0] := '5000';

    Port := StrToIntDef(PortPage.Values[0], 0);
    if (Port < 1) or (Port > 65535) then
    begin
      MsgBox('端口必须是 1 到 65535 之间的整数。', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function GetPort(Param: String): String;
begin
  Result := '';
  if Assigned(PortPage) then
    Result := Trim(PortPage.Values[0]);

  if Result = '' then
    Result := ExpandConstant('{param:PORT|5000}');
end;

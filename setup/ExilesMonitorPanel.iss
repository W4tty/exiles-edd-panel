; Exiles Monitor panel for EDDiscovery: Windows setup (Inno Setup 6).
; Built by build/package.ps1:  ISCC /DAppVersion=1.0.2 setup\ExilesMonitorPanel.iss
;
; Installs ExilesMonitorPanel.dll into EDDiscovery's DLL folder, found the same way install.ps1 finds it:
; a portable data folder next to EDDiscovery.exe (the installed copy from Windows' program list, Program Files,
; or the home rig's D:\WattysRig\Apps\EDDiscovery) wins, else %LOCALAPPDATA%\EDDiscovery\DLL.
; No admin rights: everything it writes is in the user's own folders. The uninstaller lives in
; %LOCALAPPDATA%\ExilesMonitorPanel, never in the DLL folder EDDiscovery scans.

#ifndef AppVersion
  #define AppVersion "dev"
#endif

[Setup]
AppId={{6F2C3E7A-4B1D-4E8A-9C5F-2D7B8E1A0C34}
AppName=Exiles Monitor panel for EDDiscovery
AppVersion={#AppVersion}
AppVerName=Exiles Monitor panel {#AppVersion}
AppPublisher=The Exiles
AppPublisherURL=https://reports.theexiles.gg
AppSupportURL=https://github.com/W4tty/exiles-edd-panel
DefaultDirName={code:GetDllDir}
AppendDefaultDirName=no
DirExistsWarning=no
DisableDirPage=no
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=lowest
UninstallFilesDir={localappdata}\ExilesMonitorPanel
UninstallDisplayName=Exiles Monitor panel for EDDiscovery
CreateUninstallRegKey=yes
OutputBaseFilename=ExilesMonitorPanel-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
InfoAfterFile=after-install.txt

[Messages]
SelectDirLabel3=The panel goes into EDDiscovery's DLL folder. Setup found it below; change it only if your EDDiscovery keeps its data somewhere else.
SelectDirBrowseLabel=Click Next to install there.

[Files]
Source: "..\src\ExilesMonitorPanel\bin\Release\ExilesMonitorPanel.dll"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{code:GetEddExe}"; Description: "Start EDDiscovery now"; Flags: postinstall nowait skipifsilent unchecked; Check: EddExeFound

[Code]
function IsEddRunning(): Boolean;
var
  Locator, Service, List: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    List := Service.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = ''EDDiscovery.exe''');
    Result := List.Count > 0;
  except
    Result := False;
  end;
end;

procedure AddInstalledFrom(RootKey: Integer; var Folders: TArrayOfString);
var
  Names: TArrayOfString;
  I, N: Integer;
  Key, DisplayName, Location: String;
begin
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  if not RegGetSubkeyNames(RootKey, Key, Names) then
    Exit;
  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if RegQueryStringValue(RootKey, Key + '\' + Names[I], 'DisplayName', DisplayName) and
       (Pos('EDDiscovery', DisplayName) = 1) and
       RegQueryStringValue(RootKey, Key + '\' + Names[I], 'InstallLocation', Location) and
       (Location <> '') then
    begin
      N := GetArrayLength(Folders);
      SetArrayLength(Folders, N + 1);
      Folders[N] := RemoveBackslashUnlessRoot(Location);
    end;
  end;
end;

function EddFolders(): TArrayOfString;
var
  N: Integer;
begin
  SetArrayLength(Result, 0);
  if IsWin64 then
    AddInstalledFrom(HKLM64, Result);
  AddInstalledFrom(HKLM32, Result);
  AddInstalledFrom(HKCU, Result);
  N := GetArrayLength(Result);
  SetArrayLength(Result, N + 3);
  Result[N] := ExpandConstant('{commonpf64}\EDDiscovery');
  Result[N + 1] := ExpandConstant('{commonpf32}\EDDiscovery');
  Result[N + 2] := 'D:\WattysRig\Apps\EDDiscovery';
end;

function GetDllDir(Param: String): String;
var
  Folders: TArrayOfString;
  I: Integer;
begin
  Folders := EddFolders();
  for I := 0 to GetArrayLength(Folders) - 1 do
    if FileExists(AddBackslash(Folders[I]) + 'EDDiscovery\EDDUser.sqlite') then
    begin
      Result := AddBackslash(Folders[I]) + 'EDDiscovery\DLL';
      Exit;
    end;
  Result := ExpandConstant('{localappdata}\EDDiscovery\DLL');
end;

function GetEddExe(Param: String): String;
var
  Folders: TArrayOfString;
  I: Integer;
begin
  Result := '';
  Folders := EddFolders();
  for I := 0 to GetArrayLength(Folders) - 1 do
    if FileExists(AddBackslash(Folders[I]) + 'EDDiscovery.exe') then
    begin
      Result := AddBackslash(Folders[I]) + 'EDDiscovery.exe';
      Exit;
    end;
end;

function EddExeFound(): Boolean;
begin
  Result := GetEddExe('') <> '';
end;

function WaitForEddClosed(): Boolean;
begin
  Result := True;
  while IsEddRunning() do
  begin
    if SuppressibleMsgBox('EDDiscovery is running. Close it, then click Retry.', mbError, MB_RETRYCANCEL, IDCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not WaitForEddClosed() then
    Result := 'EDDiscovery is still running. Close it and run Setup again.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := WaitForEddClosed();
end;

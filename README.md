# Exiles Monitor panel for EDDiscovery

An [EDDiscovery](https://github.com/EDDiscovery/EDDiscovery) panel DLL that shows the Exiles squad
monitor (https://reports.theexiles.gg) as a tab inside EDDiscovery, the way the Inara tab works,
and follows the system you are in.

Built and tested with EDDiscovery 19.1.11 on Windows. Needs the Microsoft Edge WebView2 Runtime,
which EDDiscovery's own Inara, EDSM and Spansh tabs need as well.

## Install

Download `ExilesMonitorPanel-<version>.zip` from the
[releases page](https://github.com/W4tty/exiles-edd-panel/releases), unzip it, close EDDiscovery and run

```
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Then start EDDiscovery, click Yes when it asks to allow `ExilesMonitorPanel.dll`, click the `+` tab and
pick "Exiles Monitor". The full steps, the manual copy and the uninstall are in `INSTALL.txt`.

## What it does

- A toolbar: Monitor (front page), Back, Reload, Follow my ship, the system you are in, Open in browser,
  Site (change the address, for ops.theexiles.gg or a test server).
- Follow my ship: the panel opens the monitor page `#system/<name>` for the system of the newest journal
  entry (EDDiscovery's history cursor), and `#system/<name>?station=<station>` when you dock. Untick it to
  browse freely; the toolbar keeps showing where you are.
- Links that want a new window open in your normal browser.
- Colours and font follow the EDDiscovery theme. Transparency is not supported (a browser cannot be see-through).
- Public monitor only: the DLL holds no keys or logins. The leader pages of the monitor use the same password
  prompt they use in a browser.

## How it fits EDDiscovery

EDDiscovery scans its `DLL` data folder at start, loads each .NET assembly and looks for a class whose name
ends in `EDDClass`. `ExilesMonitorEDDClass.EDDInitialise` registers the panel through `AddPanel`; the panel
itself is `MonitorPanel`, a WinForms `UserControl` implementing `IEDDPanelExtension` from
`EDDDLLInterfaces.dll`. EDDiscovery creates the control when the tab opens and feeds it the history cursor,
journal events and theme changes.

The browser is EDDiscovery's own WebView2 (`Microsoft.Web.WebView2` 1.0.1054.31, shipped with EDD 19.1.11):
the DLL is compiled against that build and copies nothing next to itself. Should a later EDDiscovery ship a
newer WebView2 build, an `AssemblyResolve` handler binds to whatever build the host carries. The WebView2
profile folder is EDDiscovery's own (`WebView2` under its data folder), so the panel shares one browser
process with the Inara, EDSM and Spansh tabs.

## Build

.NET SDK 8 or later on Windows (the .NET Framework 4.8 reference assemblies come from NuGet):

```
dotnet build src/ExilesMonitorPanel/ExilesMonitorPanel.csproj -c Release
powershell -ExecutionPolicy Bypass -File build/package.ps1 -Version v1.0.0
```

The output is `src/ExilesMonitorPanel/bin/Release/ExilesMonitorPanel.dll`; the package script zips it with the
install script and notes into `dist/`. A push of a `v*` tag builds the zip on GitHub Actions and attaches it
to a release.

`lib/EDDDLLInterfaces.dll` is the interface assembly from the EDDiscovery 19.1.11 portable zip (Apache 2.0),
used at compile time only.

## Testing it without the game

Start a portable EDDiscovery with its own data folder, for example
`EDDiscovery.exe -appfolder C:\edd-test -nosystemsload -nocheckrelease -noeddnfornewcommanders`, copy the
DLL into `C:\edd-test\DLL`, allow it when asked and open the panel from the `+` tab. Appending a `Docked` or
`FSDJump` line to the newest journal file in the commander's journal folder moves the panel the same way the
game does.

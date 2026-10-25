# WinMeter

CPU, memory, disk, and network in the Windows notification area. Left-click opens the panel. The right-click menu starts WinMeter with Windows or quits.

No administrator rights. This version does not read temperatures. Readings stay on the machine.

## Download

[Download WinMeter.exe](https://github.com/imariman/win-meter/releases/latest/download/WinMeter.exe) for 64-bit Windows. It includes the runtime, so .NET does not need to be installed.

Windows SmartScreen can block an unsigned download. Choose **More info**, then **Run anyway**.

## Run

The .NET 10 SDK is installed for the current user. `dotnet` on PATH is still 6, so start the project with:

```powershell
.\run.ps1
```

Print one sample:

```powershell
.\run.ps1 -- --once
```

Build a double-clickable copy:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish src\WinMeter\WinMeter.csproj -c Release -r win-x64 --self-contained true -o artifacts\win-meter
.\artifacts\win-meter\WinMeter.exe
```

If the icons are hidden, open the taskbar overflow arrow and drag CPU, RAM, DSK, and NET onto the bar.

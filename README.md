# WinMeter

Windows bildirim alanında CPU, bellek, disk ve ağ. Sol tık paneli açar. Sağ tık menüsünden Windows ile birlikte başlatılır veya uygulamadan çıkılır.

Yönetici izni gerekmez. Sıcaklık bu sürümde yok. Ölçümler makinede kalır.

## Çalıştırma

Bu bilgisayarda .NET 10 SDK kullanıcı klasörüne kurulu. Sistemdeki `dotnet` hâlâ 6 olduğu için projeyi şu komutla aç:

```powershell
.\run.ps1
```

Tek ölçüm:

```powershell
.\run.ps1 -- --once
```

Çift tıklanan sürüm:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish src\WinMeter\WinMeter.csproj -c Release -r win-x64 --self-contained true -o artifacts\win-meter
.\artifacts\win-meter\WinMeter.exe
```

Simgeler gizliyse görev çubuğundaki oka tıklayıp CPU, RAM, DSK ve NET simgelerini dışarı sürükle.

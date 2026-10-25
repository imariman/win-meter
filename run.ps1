$local = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
$dotnet = if (Test-Path $local) { $local } else { "dotnet" }
& $dotnet run --project (Join-Path $PSScriptRoot "src\WinMeter\WinMeter.csproj") -c Release @args
if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }

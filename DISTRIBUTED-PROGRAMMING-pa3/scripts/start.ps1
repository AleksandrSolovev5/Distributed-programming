$root = Split-Path -Parent $PSScriptRoot

$valuatorDir = Join-Path $root "Valuator"
$rankDir     = Join-Path $root "RankCalculator"

$nginxDir  = "C:\nginx\nginx-1.28.2"
$nginxExe  = Join-Path $nginxDir "nginx.exe"
$nginxConf = Join-Path $nginxDir "conf\nginx.conf"
$nginxLogs = Join-Path $nginxDir "logs"

$valuatorCsproj = Get-ChildItem -Path $valuatorDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $valuatorCsproj) { throw "Не найден .csproj в папке Valuator" }

$rankCsproj = Get-ChildItem -Path $rankDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $rankCsproj) { throw "Не найден .csproj в папке RankCalculator" }

if (-not (Test-Path $nginxExe)) {
    throw "nginx.exe не найден: $nginxExe"
}

if (-not (Test-Path $nginxConf)) {
    throw "nginx.conf не найден: $nginxConf"
}

if (-not (Test-Path $nginxLogs)) {
    New-Item -ItemType Directory -Path $nginxLogs | Out-Null
}

Write-Host "Останавливаю старый nginx, если он запущен..."
try { & $nginxExe -p $nginxDir -s stop 2>$null | Out-Null } catch { }
taskkill /IM nginx.exe /F 2>$null | Out-Null

Write-Host "Собираю Valuator..."
dotnet build $valuatorCsproj.FullName

Write-Host "Собираю RankCalculator..."
dotnet build $rankCsproj.FullName

Write-Host "Запускаю Valuator x4..."
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$valuatorCsproj.FullName,"--urls","http://0.0.0.0:5001") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$valuatorCsproj.FullName,"--urls","http://0.0.0.0:5002") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$valuatorCsproj.FullName,"--urls","http://0.0.0.0:5003") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$valuatorCsproj.FullName,"--urls","http://0.0.0.0:5004") -WorkingDirectory $root | Out-Null

Write-Host "Запускаю RankCalculator x4..."
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$rankCsproj.FullName) -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$rankCsproj.FullName) -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$rankCsproj.FullName) -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$rankCsproj.FullName) -WorkingDirectory $root | Out-Null

Write-Host "Запускаю nginx..."
Start-Process $nginxExe `
    -ArgumentList @("-p", $nginxDir, "-c", "conf/nginx.conf") `
    -WorkingDirectory $nginxDir | Out-Null

Start-Sleep -Seconds 1
Write-Host "Готово."
Write-Host "Valuator доступен через nginx: http://localhost:8080"
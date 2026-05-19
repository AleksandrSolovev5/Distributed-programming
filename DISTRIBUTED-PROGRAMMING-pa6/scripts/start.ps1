$root = Split-Path -Parent $PSScriptRoot

$valuatorDir = Join-Path $root "Valuator"
$rankDir     = Join-Path $root "RankCalculator"
$loggerDir   = Join-Path $root "EventsLogger"

$env:DB_MAIN = "localhost:6000"
$env:DB_RU = "localhost:6001"
$env:DB_EU = "localhost:6002"
$env:DB_ASIA = "localhost:6003"

$nginxDir  = "C:\nginx\nginx-1.28.2"
$nginxExe  = Join-Path $nginxDir "nginx.exe"
$nginxConf = Join-Path $nginxDir "conf\nginx.conf"
$nginxLogs = Join-Path $nginxDir "logs"

$valuatorCsproj = Get-ChildItem -Path $valuatorDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $valuatorCsproj) { throw "Не найден .csproj в папке Valuator" }

$rankCsproj = Get-ChildItem -Path $rankDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $rankCsproj) { throw "Не найден .csproj в папке RankCalculator" }

$loggerCsproj = Get-ChildItem -Path $loggerDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $loggerCsproj) { throw "Не найден .csproj в папке EventsLogger" }

if (-not (Test-Path $nginxExe)) {
    throw "nginx.exe не найден: $nginxExe"
}

if (-not (Test-Path $nginxConf)) {
    throw "nginx.conf не найден: $nginxConf"
}

if (-not (Test-Path $nginxLogs)) {
    New-Item -ItemType Directory -Path $nginxLogs | Out-Null
}

Write-Host "Останавливаю старые процессы, если они запущены..."

try { & $nginxExe -p $nginxDir -s stop 2>$null | Out-Null } catch { }
taskkill /IM nginx.exe /F 2>$null | Out-Null

$ports = @(5001, 5002, 5003, 5004)
foreach ($port in $ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        try {
            Stop-Process -Id $connection.OwningProcess -Force -ErrorAction SilentlyContinue
        } catch {
        }
    }
}

$rankProcs = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "RankCalculator" }

foreach ($p in $rankProcs) {
    try {
        $null = Invoke-CimMethod -InputObject $p -MethodName Terminate
    } catch {
    }
}

$loggerProcs = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "EventsLogger" }

foreach ($p in $loggerProcs) {
    try {
        $null = Invoke-CimMethod -InputObject $p -MethodName Terminate
    } catch {
    }
}

Write-Host "Собираю Valuator..."
dotnet build $valuatorCsproj.FullName

Write-Host "Собираю RankCalculator..."
dotnet build $rankCsproj.FullName

Write-Host "Собираю EventsLogger..."
dotnet build $loggerCsproj.FullName

Write-Host "Запускаю Valuator x2..."
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$valuatorCsproj.FullName,"--urls","http://0.0.0.0:5001") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$valuatorCsproj.FullName,"--urls","http://0.0.0.0:5002") -WorkingDirectory $root | Out-Null

Write-Host "Запускаю RankCalculator x2..."
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$rankCsproj.FullName) -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$rankCsproj.FullName) -WorkingDirectory $root | Out-Null

Write-Host "Запускаю EventsLogger x2..."
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$loggerCsproj.FullName) -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--no-build","--project",$loggerCsproj.FullName) -WorkingDirectory $root | Out-Null

Write-Host "Запускаю nginx..."
Start-Process $nginxExe `
    -ArgumentList @("-p", $nginxDir, "-c", "conf/nginx.conf") `
    -WorkingDirectory $nginxDir | Out-Null

Start-Sleep -Seconds 1
Write-Host "Готово."
Write-Host "Valuator доступен через nginx: http://localhost:8080"
Write-Host "Запущено:"
Write-Host "Valuator x2:        5001, 5002"
Write-Host "RankCalculator x2"
Write-Host "EventsLogger x1"
Write-Host "Redis shards:"
Write-Host "DB_MAIN = $env:DB_MAIN"
Write-Host "DB_RU = $env:DB_RU"
Write-Host "DB_EU = $env:DB_EU"
Write-Host "DB_ASIA = $env:DB_ASIA"
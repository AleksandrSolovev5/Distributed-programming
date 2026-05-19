$root = Split-Path -Parent $PSScriptRoot

$env:ASPNETCORE_ENVIRONMENT = "Development"

$env:REDIS_CONNECTION = "localhost:6380,password=redis-password"

$env:RABBITMQ_HOST = "localhost"
$env:RABBITMQ_PORT = "5673"
$env:RABBITMQ_USER = "valuator"
$env:RABBITMQ_PASSWORD = "rabbitmq-password"

$valuatorDir = Join-Path $root "Valuator"
$rankDir     = Join-Path $root "RankCalculator"
$loggerDir   = Join-Path $root "EventsLogger"

$valuatorCsproj = Get-ChildItem -Path $valuatorDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $valuatorCsproj) {
    throw "Не найден .csproj в папке Valuator"
}

$rankCsproj = Get-ChildItem -Path $rankDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $rankCsproj) {
    throw "Не найден .csproj в папке RankCalculator"
}

$loggerCsproj = Get-ChildItem -Path $loggerDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $loggerCsproj) {
    throw "Не найден .csproj в папке EventsLogger"
}

Write-Host "Останавливаю старые процессы, если они запущены..."

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

if ($LASTEXITCODE -ne 0) {
    throw "Ошибка сборки Valuator"
}

Write-Host "Собираю RankCalculator..."
dotnet build $rankCsproj.FullName

if ($LASTEXITCODE -ne 0) {
    throw "Ошибка сборки RankCalculator"
}

Write-Host "Собираю EventsLogger..."
dotnet build $loggerCsproj.FullName

if ($LASTEXITCODE -ne 0) {
    throw "Ошибка сборки EventsLogger"
}

Write-Host "Запускаю Valuator..."
Start-Process dotnet `
    -ArgumentList @("run", "--no-build", "--project", $valuatorCsproj.FullName, "--urls", "http://localhost:5001") `
    -WorkingDirectory $root

Write-Host "Запускаю RankCalculator..."
Start-Process dotnet `
    -ArgumentList @("run", "--no-build", "--project", $rankCsproj.FullName) `
    -WorkingDirectory $root

Write-Host "Запускаю EventsLogger..."
Start-Process dotnet `
    -ArgumentList @("run", "--no-build", "--project", $loggerCsproj.FullName) `
    -WorkingDirectory $root

Start-Sleep -Seconds 3

Write-Host "Открываю браузер..."
Start-Process "http://localhost:5001"

Write-Host "Готово."
Write-Host "Valuator доступен: http://localhost:5001"
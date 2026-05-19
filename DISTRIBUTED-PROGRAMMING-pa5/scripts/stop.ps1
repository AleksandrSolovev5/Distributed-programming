$root = Split-Path -Parent $PSScriptRoot

$nginxDir = "C:\nginx\nginx-1.28.2"
$nginxExe = Join-Path $nginxDir "nginx.exe"

Write-Host "Останавливаю nginx..."
try { & $nginxExe -p $nginxDir -s stop 2>$null | Out-Null } catch { }
taskkill /IM nginx.exe /F 2>$null | Out-Null

$ports = @(5001, 5002)
foreach ($port in $ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        try {
            Stop-Process -Id $connection.OwningProcess -Force -ErrorAction SilentlyContinue
        } catch {
        }
    }
}

Write-Host "Останавливаю Valuator..."
$valuatorProcs = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "Valuator" }

foreach ($p in $valuatorProcs) {
    try {
        $null = Invoke-CimMethod -InputObject $p -MethodName Terminate
    } catch {
    }
}

Write-Host "Останавливаю RankCalculator..."
$rankProcs = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "RankCalculator" }

foreach ($p in $rankProcs) {
    try {
        $null = Invoke-CimMethod -InputObject $p -MethodName Terminate
    } catch {
    }
}

Write-Host "Останавливаю EventsLogger..."
$loggerProcs = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "EventsLogger" }

foreach ($p in $loggerProcs) {
    try {
        $null = Invoke-CimMethod -InputObject $p -MethodName Terminate
    } catch {
    }
}

Write-Host "Готово. Все сервисы остановлены."
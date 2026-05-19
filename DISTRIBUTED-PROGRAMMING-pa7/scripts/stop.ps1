Write-Host "Останавливаю Valuator..."

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

Write-Host "Готово."
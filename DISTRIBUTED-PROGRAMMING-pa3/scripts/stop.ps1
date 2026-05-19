$root = Split-Path -Parent $PSScriptRoot

$nginxDir = "C:\nginx\nginx-1.28.2"
$nginxExe = Join-Path $nginxDir "nginx.exe"

Write-Host "Останавливаю Valuator на портах 5001-5004..."
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

Write-Host "Останавливаю nginx..."
if (Test-Path $nginxExe) {
    try {
        & $nginxExe -p $nginxDir -s stop 2>$null | Out-Null
    } catch {
    }
}

taskkill /IM nginx.exe /F 2>$null | Out-Null

Write-Host "Готово."
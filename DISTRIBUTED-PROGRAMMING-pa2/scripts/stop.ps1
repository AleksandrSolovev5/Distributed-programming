$nginxDir = "C:\nginx\nginx-1.28.2"
$nginxExe = Join-Path $nginxDir "nginx.exe"

function Stop-ByPort($port) {
    $lines = netstat -ano | Select-String ":$port\s+.*(LISTENING|ОЖИДАНИЕ)" # получение списка процессов по порту
    foreach ($l in $lines) {
        $parts = ($l.ToString() -split "\s+")
        $procId = $parts[-1] # находим PID процесса 
        if ($procId -match "^\d+$") {
            taskkill /PID $procId /F | Out-Null # команда для принудительного завершения процесса по его PID
        }
    }
}

Stop-ByPort 5001
Stop-ByPort 5002
Stop-ByPort 5003
Stop-ByPort 5004

try { & $nginxExe -s stop | Out-Null } catch { } # попытка корректной остановки
taskkill /IM nginx.exe /F | Out-Null  # принудительное завершение

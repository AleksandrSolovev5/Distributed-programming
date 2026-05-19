$root = Split-Path -Parent $PSScriptRoot
$valuatorDir = Join-Path $root "Valuator"

$nginxDir  = "C:\nginx\nginx-1.28.2"
$nginxExe  = Join-Path $nginxDir "nginx.exe"
$nginxConf = Join-Path $nginxDir "conf\nginx.conf"

$csproj = Get-ChildItem -Path $valuatorDir -Recurse -Filter "*.csproj" | Select-Object -First 1
if (-not $csproj) { throw "Не найден .csproj в папке Valuator" }

# остановка nginx, если запущен
try { & $nginxExe -s stop | Out-Null } catch { }
taskkill /IM nginx.exe /F | Out-Null

Start-Process dotnet -ArgumentList @("run","--project",$csproj.FullName,"--urls","http://0.0.0.0:5001") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--project",$csproj.FullName,"--urls","http://0.0.0.0:5002") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--project",$csproj.FullName,"--urls","http://0.0.0.0:5003") -WorkingDirectory $root | Out-Null
Start-Process dotnet -ArgumentList @("run","--project",$csproj.FullName,"--urls","http://0.0.0.0:5004") -WorkingDirectory $root | Out-Null


# запуск nginx
Start-Process $nginxExe -ArgumentList @("-c",$nginxConf) -WorkingDirectory $nginxDir | Out-Null

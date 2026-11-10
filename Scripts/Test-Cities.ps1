$ErrorActionPreference = 'Stop'
Write-Host 'Consultando cidades com viagens...' -ForegroundColor Cyan
$raw = Invoke-RestMethod 'http://localhost:5263/api/locations/available' -TimeoutSec 10
$cities = @($raw | ForEach-Object { $_ })
Write-Host "Total: $($cities.Count)" -ForegroundColor Green
$cities | Sort-Object uf,nome | Format-Table id,nome,uf -AutoSize
if ($cities.Count -lt 8) { exit 1 }

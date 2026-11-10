[CmdletBinding()]
param([switch]$RequireDemoData)
$ErrorActionPreference = 'Stop'
Write-Host 'Testando RotaWeb integrado em http://localhost:5263...' -ForegroundColor Cyan

$health = Invoke-RestMethod 'http://localhost:5263/api/security/health' -TimeoutSec 5
if ($health.status -ne 'ok') { throw 'Healthcheck falhou.' }
Write-Host '[OK] Backend' -ForegroundColor Green
$ready = Invoke-RestMethod 'http://localhost:5263/api/security/ready' -TimeoutSec 10
if ($ready.status -ne 'ready') { throw 'Ao menos um dos bancos do RotaWeb não está pronto.' }
Write-Host '[OK] Três bancos acessíveis' -ForegroundColor Green

# CRUD administrativo altera dados: executar separadamente e somente em banco de teste.
$unauthenticated = $false
try {
    Invoke-WebRequest 'http://localhost:5263/api/admin/auth/me' -UseBasicParsing -TimeoutSec 5 | Out-Null
} catch {
    $status = [int]$_.Exception.Response.StatusCode
    if ($status -eq 401) { $unauthenticated = $true } else { throw }
}
if (-not $unauthenticated) { throw 'Rota administrativa acessível sem autenticação.' }
Write-Host '[OK] Admin protegido contra acesso não autenticado' -ForegroundColor Green

$citiesRaw = Invoke-RestMethod 'http://localhost:5263/api/locations/available' -TimeoutSec 10
$cities = @($citiesRaw | ForEach-Object { $_ })
if ($RequireDemoData -and $cities.Count -lt 1) { throw "Dados demonstrativos ausentes: cidades com viagens = $($cities.Count)." }
Write-Host "[OK] Cidades com viagens: $($cities.Count)" -ForegroundColor Green

$date = (Get-Date).Date.AddDays(1).ToString('yyyy-MM-dd')
$tripsRaw = Invoke-RestMethod "http://localhost:5263/api/trips?origemId=3550308&destinoId=3304557&data=$date&categoria=Todas&ordenar=horario" -TimeoutSec 10
$trips = @($tripsRaw | ForEach-Object { $_ })
if ($RequireDemoData -and $trips.Count -lt 1) { throw 'Nenhuma viagem demonstrativa São Paulo -> Rio encontrada.' }
Write-Host "[OK] São Paulo -> Rio: $($trips.Count) viagens" -ForegroundColor Green

$page = Invoke-WebRequest 'http://localhost:5263/' -UseBasicParsing -TimeoutSec 5
if ($page.StatusCode -ne 200 -or $page.Content -notmatch '<div id="root"') { throw 'Frontend integrado falhou.' }
Write-Host '[OK] Frontend React servido pelo ASP.NET Core' -ForegroundColor Green

Write-Host 'Verificações HTTP essenciais concluídas. Não foram feitas mutações no banco.' -ForegroundColor Green


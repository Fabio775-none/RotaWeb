param([string]$AdminPassword)

$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5263/api'
. (Join-Path $PSScriptRoot 'Shared/AdminAuthentication.ps1')
Write-Host 'Testando operações de gravação em banco de testes...' -ForegroundColor Cyan
$confirm = Read-Host 'Esta verificação criará/editará/excluirá dados. Digite TESTE para continuar'
if ($confirm -cne 'TESTE') { throw 'Execução cancelada para proteger o banco.' }
$auth = Connect-RotaWebAdmin -ApiBase $base -Password $AdminPassword
$session = $auth.Session
$headers = $auth.Headers
Write-Host '[OK] Senha e sessão administrativa validadas' -ForegroundColor Green

$suffix = Get-Random -Minimum 1000 -Maximum 9999
$prefix = "TST-$suffix"
$plate = "TST$suffix"

$busBody = @{
    prefixo = $prefix
    placa = $plate
    modelo = 'Onibus Teste CRUD'
    categoria = 'Executivo'
    assentos = 30
} | ConvertTo-Json

$bus = $null
$trip = $null
$completed = $false
try {
    $bus = Invoke-RestMethod -Uri "$base/admin/buses" -Method Post -ContentType 'application/json' -Headers $headers -Body $busBody -WebSession $session -TimeoutSec 10
    if (-not $bus.id) { throw 'Cadastro de ônibus não retornou ID.' }
    Write-Host "[OK] CREATE ônibus #$($bus.id)" -ForegroundColor Green
    
    $busUpdateBody = @{
        prefixo = $prefix
        placa = $plate
        modelo = 'Onibus Teste CRUD Editado'
        categoria = 'Semi-leito'
        assentos = 30
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$base/admin/buses/$($bus.id)" -Method Put -ContentType 'application/json' -Headers $headers -Body $busUpdateBody -WebSession $session -TimeoutSec 10 | Out-Null
    Write-Host '[OK] UPDATE ônibus' -ForegroundColor Green
    
    $startLocal = (Get-Date).Date.AddDays(40).AddHours(9)
    $endLocal = $startLocal.AddHours(3)
    $tripBody = @{
        onibusId = [int]$bus.id
        origemId = 3550308
        destinoId = 3509502
        partida = $startLocal.ToUniversalTime().ToString('o')
        chegada = $endLocal.ToUniversalTime().ToString('o')
        precoCentavos = 12345
    } | ConvertTo-Json
    
    $trip = Invoke-RestMethod -Uri "$base/admin/trips" -Method Post -ContentType 'application/json' -Headers $headers -Body $tripBody -WebSession $session -TimeoutSec 20
    if (-not $trip.id) { throw 'Cadastro de viagem não retornou ID.' }
    Write-Host "[OK] CREATE viagem #$($trip.id)" -ForegroundColor Green
    
    $tripUpdateBody = @{
        onibusId = [int]$bus.id
        origemId = 3550308
        destinoId = 3509502
        partida = $startLocal.ToUniversalTime().ToString('o')
        chegada = $endLocal.ToUniversalTime().ToString('o')
        precoCentavos = 13500
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$base/admin/trips/$($trip.id)" -Method Put -ContentType 'application/json' -Headers $headers -Body $tripUpdateBody -WebSession $session -TimeoutSec 20 | Out-Null
    Write-Host '[OK] UPDATE viagem/preço' -ForegroundColor Green
    
    Invoke-RestMethod -Uri "$base/admin/trips/$($trip.id)" -Method Delete -Headers $headers -WebSession $session -TimeoutSec 10 | Out-Null
    Write-Host '[OK] DELETE viagem' -ForegroundColor Green
    
    Invoke-RestMethod -Uri "$base/admin/buses/$($bus.id)" -Method Delete -Headers $headers -WebSession $session -TimeoutSec 10 | Out-Null
    Write-Host '[OK] DELETE ônibus' -ForegroundColor Green
    
    Write-Host 'CRUD ADMINISTRATIVO VALIDADO.' -ForegroundColor Green

    $completed = $true
}
finally {
    # Remove apenas os registros temporários criados nesta execução.
    # Se a operação principal falhar, não escondemos esse erro durante a limpeza.
    if (-not $completed) {
        if ($trip -and $trip.id) {
            try { Invoke-RestMethod -Uri "$base/admin/trips/$($trip.id)" -Method Delete -Headers $headers -WebSession $session -TimeoutSec 10 | Out-Null }
            catch { Write-Warning "A viagem temporária $($trip.id) precisa de limpeza manual: $($_.Exception.Message)" }
        }
        if ($bus -and $bus.id) {
            try { Invoke-RestMethod -Uri "$base/admin/buses/$($bus.id)" -Method Delete -Headers $headers -WebSession $session -TimeoutSec 10 | Out-Null }
            catch { Write-Warning "O ônibus temporário $($bus.id) precisa de limpeza manual: $($_.Exception.Message)" }
        }
    }
}

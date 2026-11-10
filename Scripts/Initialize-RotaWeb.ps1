# RotaWeb - compilação e inicialização integrada (uso local).
# Compila o React e o integra ao ASP.NET Core. Depois disso, frontend e API
# rodam na MESMA origem (http://localhost:5263), eliminando CORS/proxy/Vite
# da execução integrada.
# Para CRUD administrativo, use Test-AdminCrud.ps1 explicitamente.


$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root 'backend'
$frontend = Join-Path $root 'frontend'
$wwwroot = Join-Path $backend 'wwwroot'

function Assert-Command([string]$name, [string]$hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name não foi encontrado. $hint"
    }
}

function Assert-PortAvailable([int]$port) {
    $listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        throw "A porta $port já está em uso. Encerre o servidor antigo manualmente antes de iniciar."
    }
}

Assert-Command 'dotnet' 'Instale o .NET SDK 10.'
Assert-Command 'npm' 'Instale o Node.js.'

# Validação rápida do código-fonte crítico da busca de viagens.
$homeSource = Get-Content (Join-Path $frontend 'src\pages\Home.jsx') -Raw
if ($homeSource -notmatch 'data\s*:\s*date') {
    throw 'Home.jsx não contém o parâmetro data: date na busca de viagens.'
}

Write-Host ''
Write-Host '=====================================================' -ForegroundColor Cyan
Write-Host ' RotaWeb - INICIALIZAÇÃO INTEGRADA' -ForegroundColor Cyan
Write-Host ' Frontend + API na mesma porta 5263' -ForegroundColor Cyan
Write-Host '=====================================================' -ForegroundColor Cyan

# Protege processos já existentes na porta integrada.
Assert-PortAvailable 5263

# Testa e compila a interface antes de substituir a versão existente.
Write-Host ''
Write-Host '[1/4] Testando e compilando frontend React...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'Build-RotaWeb.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Falha no build do frontend.' }
Write-Host '[2/4] Frontend integrado com segurança.' -ForegroundColor Green

# Inicia somente o ASP.NET Core para servir a aplicação integrada.
Write-Host '[3/4] Iniciando RotaWeb...' -ForegroundColor Cyan
Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$backend'; dotnet run"

$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 1
    try {
        $health = Invoke-RestMethod -Uri 'http://localhost:5263/api/security/health' -TimeoutSec 2
        if ($health.status -eq 'ok') {
            $ready = $true
            break
        }
    }
    catch { }
}
if (-not $ready) {
    throw 'O backend não respondeu em 60 segundos. Veja a janela do dotnet run.'
}
Write-Host 'Backend OK.' -ForegroundColor Green

# O startup não executa login administrativo nem mutações de CRUD automaticamente.
# Os testes administrativos são opt-in e exigem login com senha.
Write-Host 'Admin: acesso por e-mail e senha com sessão protegida.' -ForegroundColor Cyan

# Confirma a lista de cidades com rotas preparadas.
$citiesRaw = Invoke-RestMethod -Uri 'http://localhost:5263/api/locations/available' -TimeoutSec 10
# No Windows PowerShell 5.1, Invoke-RestMethod pode devolver um array JSON como um único objeto de pipeline.
# Reenviamos o valor ao pipeline para contar cada cidade corretamente.
$cities = @($citiesRaw | ForEach-Object { $_ })
if ($cities.Count -lt 8) {
    Write-Warning "Existem $($cities.Count) cidades com viagens; os dados existentes foram preservados."
}
Write-Host "Cidades OK: $($cities.Count) cidades com viagens." -ForegroundColor Green
$cities | ForEach-Object { Write-Host "  - $($_.nome) - $($_.uf)" -ForegroundColor Gray }

# Testa exatamente a rota que será validada.
$date = (Get-Date).Date.AddDays(1).ToString('yyyy-MM-dd')
$tripUrl = "http://localhost:5263/api/trips?origemId=3550308&destinoId=3304557&data=$date&categoria=Todas&ordenar=horario"
$tripsRaw = Invoke-RestMethod -Uri $tripUrl -TimeoutSec 10
$trips = @($tripsRaw | ForEach-Object { $_ })
if ($trips.Count -lt 1) {
    Write-Warning "Não há viagem São Paulo -> Rio em $date. A API respondeu e nenhum dado foi alterado."
} else {
    Write-Host "Viagens OK: $($trips.Count) encontradas para $date." -ForegroundColor Green
}

# Confirma que a própria API está servindo o React.
$response = Invoke-WebRequest -Uri 'http://localhost:5263/' -UseBasicParsing -TimeoutSec 5
if ($response.StatusCode -ne 200 -or $response.Content -notmatch '<div id="root"') {
    throw 'O backend iniciou, mas não está servindo o frontend React corretamente.'
}
Write-Host 'Frontend integrado OK.' -ForegroundColor Green

Write-Host '[4/4] APLICAÇÃO PRONTA.' -ForegroundColor Green
Write-Host ''
Write-Host 'Abra sempre: http://localhost:5263' -ForegroundColor Cyan
Write-Host 'A execução integrada usa a porta 5263.' -ForegroundColor Yellow
Write-Host ''
Write-Host "Teste de viagem: São Paulo -> Rio de Janeiro em $date" -ForegroundColor Gray

Start-Process 'http://localhost:5263'

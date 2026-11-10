# Compila o frontend e atualiza wwwroot apenas depois de um build íntegro.
# Não acessa o banco de dados e não inicia a aplicação.
[CmdletBinding()]
param([switch]$SkipInstall)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root 'frontend'
$backend = Join-Path $root 'backend'
$wwwroot = Join-Path $backend 'wwwroot'
$staging = Join-Path $backend ('.wwwroot-next-' + [Guid]::NewGuid().ToString('N'))
$backup = Join-Path $backend ('.wwwroot-previous-' + [Guid]::NewGuid().ToString('N'))

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'npm não encontrado. Instale Node.js 22 LTS.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet não encontrado. Instale .NET SDK 10.' }

Push-Location $frontend
try {
    if (-not $SkipInstall) {
        # npm ci é reprodutível: utiliza as versões exatas do package-lock.
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Falha na instalação das dependências npm (npm ci).' }
    } elseif (-not (Test-Path (Join-Path $frontend 'node_modules'))) {
        throw '-SkipInstall requer frontend/node_modules já instalado.'
    }
    & npm test
    if ($LASTEXITCODE -ne 0) { throw 'Testes do frontend falharam. wwwroot não foi alterado.' }
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Build do frontend falhou. wwwroot não foi alterado.' }
} finally { Pop-Location }

$index = Join-Path $frontend 'dist/index.html'
if (-not (Test-Path $index)) { throw 'Build incompleto: dist/index.html ausente.' }
$hasJs = @(Get-ChildItem (Join-Path $frontend 'dist') -Recurse -File -Filter '*.js').Count -gt 0
if (-not $hasJs) { throw 'Build incompleto: nenhum JavaScript foi gerado.' }

try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -Path (Join-Path $frontend 'dist/*') -Destination $staging -Recurse -Force
    if (-not (Test-Path (Join-Path $staging 'index.html'))) { throw 'Cópia do frontend incompleta.' }
    if (Test-Path $wwwroot) { Move-Item -LiteralPath $wwwroot -Destination $backup }
    try {
        Move-Item -LiteralPath $staging -Destination $wwwroot
    } catch {
        if ((Test-Path $backup) -and -not (Test-Path $wwwroot)) {
            Move-Item -LiteralPath $backup -Destination $wwwroot
        }
        throw
    }
    Write-Host 'Frontend testado e integrado em backend/wwwroot.' -ForegroundColor Green
} finally {
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    if ((Test-Path $backup) -and (Test-Path (Join-Path $wwwroot 'index.html'))) {
        Remove-Item $backup -Recurse -Force
    }
}

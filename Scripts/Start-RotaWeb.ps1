# Inicia a aplicação integrada já compilada, sem reconstruir o frontend.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root 'backend'
$index = Join-Path $backend 'wwwroot\index.html'
if (-not (Test-Path $index)) {
    throw 'Frontend integrado não encontrado. Execute primeiro .\Scripts\Initialize-RotaWeb.ps1 na raiz do projeto.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet não encontrado. Instale o .NET SDK 10.'
}
Write-Host 'Iniciando RotaWeb em http://localhost:5263 ...' -ForegroundColor Cyan
Push-Location $backend
try {
    dotnet run
    if ($LASTEXITCODE -ne 0) { throw "dotnet run terminou com código $LASTEXITCODE." }
}
finally {
    Pop-Location
}

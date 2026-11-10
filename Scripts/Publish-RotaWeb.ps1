# Compilação e publicação. Não faz alterações no PostgreSQL.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root 'backend'
$publish = Join-Path $root 'publish'
$next = Join-Path $root ('publish-next-' + [Guid]::NewGuid().ToString('N'))
$old = Join-Path $root ('publish-previous-' + [Guid]::NewGuid().ToString('N'))

& (Join-Path $PSScriptRoot 'Build-RotaWeb.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Falha na compilação do frontend.' }
try {
    & dotnet publish (Join-Path $backend 'backend.csproj') -c Release -o $next
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou; publicação anterior preservada.' }
    if (-not (Test-Path (Join-Path $next 'wwwroot/index.html'))) { throw 'Publicação sem a interface React.' }
    if (Test-Path $publish) { Move-Item -LiteralPath $publish -Destination $old }
    try {
        Move-Item -LiteralPath $next -Destination $publish
    } catch {
        if ((Test-Path $old) -and -not (Test-Path $publish)) { Move-Item -LiteralPath $old -Destination $publish }
        throw
    }
    Write-Host "Publicação pronta em: $publish" -ForegroundColor Green
    Write-Host 'Configure as três conexões por variáveis de ambiente no servidor. Não publique chaves locais.' -ForegroundColor Yellow
} finally {
    if (Test-Path $next) { Remove-Item $next -Force -Recurse }
    if ((Test-Path $old) -and (Test-Path $publish)) { Remove-Item $old -Force -Recurse }
}

param([string]$AdminPassword)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Shared/AdminAuthentication.ps1')
$auth = Connect-RotaWebAdmin -Password $AdminPassword
Write-Host '[OK] Login administrativo com senha e sessão autenticada.' -ForegroundColor Green
Write-Host "E-mail: $($auth.Email)" -ForegroundColor Cyan

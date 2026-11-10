# Funções compartilhadas para testes administrativos locais. Não altera dados por si só.
function Connect-RotaWebAdmin {
    param(
        [string]$ApiBase = 'http://localhost:5263/api',
        [string]$Email,
        [string]$Password
    )

    if ([string]::IsNullOrWhiteSpace($Email)) {
        $Email = if ($env:ROTAWEB_ADMIN_EMAIL) { $env:ROTAWEB_ADMIN_EMAIL } else { 'admin@rotaweb.local' }
    }
    if ([string]::IsNullOrWhiteSpace($Password)) { $Password = $env:ROTAWEB_ADMIN_PASSWORD }
    if ([string]::IsNullOrWhiteSpace($Password)) {
        $securePassword = Read-Host 'Senha administrativa' -AsSecureString
        $credential = [System.Management.Automation.PSCredential]::new($Email, $securePassword)
        $Password = $credential.GetNetworkCredential().Password
    }
    if ([string]::IsNullOrWhiteSpace($Password)) { throw 'Informe uma senha administrativa válida.' }

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $csrf = Invoke-RestMethod -Uri "$ApiBase/security/csrf" -WebSession $session -TimeoutSec 10
    if (-not $csrf.token) { throw 'CSRF não foi emitido pela API.' }
    $headers = @{ 'X-CSRF-TOKEN' = [string]$csrf.token }
    $body = @{ email = $Email; senha = $Password } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$ApiBase/admin/auth/login" -Method Post -ContentType 'application/json' -Headers $headers -Body $body -WebSession $session -TimeoutSec 10

    if (-not $login.mensagem) { throw 'A API não confirmou o login administrativo.' }

    $me = Invoke-RestMethod -Uri "$ApiBase/admin/auth/me" -WebSession $session -TimeoutSec 10
    if (-not $me) { throw 'Sessão admin não foi confirmada.' }
    $csrf = Invoke-RestMethod -Uri "$ApiBase/security/admin-csrf" -WebSession $session -TimeoutSec 10
    if (-not $csrf.token) { throw 'CSRF autenticado não foi emitido.' }
    return [PSCustomObject]@{
        Session = $session
        Headers = @{ 'X-CSRF-TOKEN' = [string]$csrf.token }
        Email = $Email
    }
}

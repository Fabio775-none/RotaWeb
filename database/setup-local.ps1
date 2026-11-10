<#
RotaWeb - preparação local dos três bancos PostgreSQL.
O script não grava a senha do superusuário em arquivo e gera senhas aleatórias para as contas da aplicação.
#>

# Interrompe imediatamente em qualquer erro para evitar configuração parcial silenciosa.
[CmdletBinding()]
param([string]$PsqlPath)
$ErrorActionPreference = 'Stop'
if (-not $PsqlPath) {
    $command = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($command) { $PsqlPath = $command.Source }
    if (-not $PsqlPath) {
        foreach ($version in @(18,17,16,15,14)) {
            $candidate = "C:\Program Files\PostgreSQL\$version\bin\psql.exe"
            if (Test-Path $candidate) { $PsqlPath = $candidate; break }
        }
    }
}
if (-not $PsqlPath -or -not (Test-Path $PsqlPath)) {
    throw 'psql.exe não encontrado. Instale PostgreSQL (cliente psql) ou use -PsqlPath com o caminho do executável.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet não encontrado. Instale o .NET SDK 10.'
}

# Procura o cliente psql no PATH e nas instalações convencionais do Windows.
# Se houver várias instalações, informe -PsqlPath com o caminho explícito.


# Função que converte SecureString para texto somente durante o comando que precisa da senha.
function Convert-SecureStringToPlainText([Security.SecureString]$SecureValue) {
    # Usa credencial temporária apenas em memória do processo atual.
    return [System.Net.NetworkCredential]::new('', $SecureValue).Password
}

# Função que cria senha aleatória URL-safe de 32 bytes para cada conta técnica.
function New-RandomPassword {
    # Compatível com Windows PowerShell 5.1 e PowerShell 7+.
    # Cria um gerador criptograficamente seguro e preenche 32 bytes aleatórios.
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    # Base64URL remove caracteres que complicam connection strings e SQL.
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Solicita somente a senha do superusuário local; ela não será salva no projeto.
$postgresSecure = Read-Host 'Senha atual do usuário postgres' -AsSecureString
$postgresPassword = Convert-SecureStringToPlainText $postgresSecure

# Gera credenciais técnicas independentes para cada banco.
$dataPassword = New-RandomPassword
$passengerPassword = New-RandomPassword
$adminDbPassword = New-RandomPassword

# Cria um arquivo SQL temporário porque CREATE DATABASE precisa ser executado fora de uma transação explícita.
$tempSql = Join-Path $env:TEMP "rotaweb-setup-$([Guid]::NewGuid().ToString('N')).sql"

# Usa here-string de aspas simples para preservar literalmente os delimitadores $$ do PostgreSQL.
# As senhas são inseridas depois por substituição de marcadores e são removidas junto com o arquivo temporário.
$sqlTemplate = @'
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rotaweb_data_app') THEN
        CREATE ROLE rotaweb_data_app LOGIN PASSWORD '{{DATA_PASSWORD}}';
    ELSE
        ALTER ROLE rotaweb_data_app WITH LOGIN PASSWORD '{{DATA_PASSWORD}}';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rotaweb_auth_app') THEN
        CREATE ROLE rotaweb_auth_app LOGIN PASSWORD '{{PASSENGER_PASSWORD}}';
    ELSE
        ALTER ROLE rotaweb_auth_app WITH LOGIN PASSWORD '{{PASSENGER_PASSWORD}}';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rotaweb_admin_app') THEN
        CREATE ROLE rotaweb_admin_app LOGIN PASSWORD '{{ADMIN_DB_PASSWORD}}';
    ELSE
        ALTER ROLE rotaweb_admin_app WITH LOGIN PASSWORD '{{ADMIN_DB_PASSWORD}}';
    END IF;
END
$$;

SELECT 'CREATE DATABASE rotaweb_data OWNER rotaweb_data_app'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'rotaweb_data')\gexec

SELECT 'CREATE DATABASE rotaweb_auth OWNER rotaweb_auth_app'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'rotaweb_auth')\gexec

SELECT 'CREATE DATABASE rotaweb_admin_auth OWNER rotaweb_admin_app'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'rotaweb_admin_auth')\gexec

ALTER DATABASE rotaweb_data OWNER TO rotaweb_data_app;
ALTER DATABASE rotaweb_auth OWNER TO rotaweb_auth_app;
ALTER DATABASE rotaweb_admin_auth OWNER TO rotaweb_admin_app;

REVOKE CONNECT ON DATABASE rotaweb_data FROM PUBLIC;
REVOKE CONNECT ON DATABASE rotaweb_auth FROM PUBLIC;
REVOKE CONNECT ON DATABASE rotaweb_admin_auth FROM PUBLIC;

GRANT CONNECT ON DATABASE rotaweb_data TO rotaweb_data_app;
GRANT CONNECT ON DATABASE rotaweb_auth TO rotaweb_auth_app;
GRANT CONNECT ON DATABASE rotaweb_admin_auth TO rotaweb_admin_app;
'@

# Substitui somente marcadores conhecidos; as senhas técnicas são Base64URL e não contêm aspas simples.
$sqlText = $sqlTemplate.Replace('{{DATA_PASSWORD}}', $dataPassword)
$sqlText = $sqlText.Replace('{{PASSENGER_PASSWORD}}', $passengerPassword)
$sqlText = $sqlText.Replace('{{ADMIN_DB_PASSWORD}}', $adminDbPassword)
$utf8SemBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($tempSql, $sqlText, $utf8SemBom)

try {
    # PGPASSWORD vale somente para este processo e seus filhos; não é persistido no projeto.
    $env:PGPASSWORD = $postgresPassword

    # Proteção contra sobrescrita: este instalador só prepara uma máquina nova.
    # Uma nova execução sobre bases existentes poderia alterar senhas dos usuários técnicos.
    $countSql = @'
SELECT
  (SELECT count(*) FROM pg_database WHERE datname IN ('rotaweb_data','rotaweb_auth','rotaweb_admin_auth'))
  + (SELECT count(*) FROM pg_roles WHERE rolname IN ('rotaweb_data_app','rotaweb_auth_app','rotaweb_admin_app'));
'@
    $preflight = & $PsqlPath -X -t -A -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -c $countSql
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar os bancos existentes. Nada foi alterado.' }
    $existingCount = 0
    if (-not [int]::TryParse(([string]($preflight | Select-Object -Last 1)).Trim(), [ref]$existingCount)) {
        throw 'Não foi possível confirmar se a instalação é nova. Nada foi alterado.'
    }
    if ($existingCount -gt 0) {
        throw 'Uma instalação RotaWeb já existe ou está parcialmente configurada. O instalador não altera bancos, usuários ou senhas existentes. Use suas connection strings atuais e consulte o README para migração entre máquinas.'
    }

    # Executa a preparação usando o banco postgres como banco administrativo inicial.
    & $PsqlPath -X -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f $tempSql

    if ($LASTEXITCODE -ne 0) {
        throw "Falha na preparação do PostgreSQL. Código: $LASTEXITCODE"
    }

    # Entra na pasta do backend para gravar connection strings em User Secrets do .NET.
    $backendPath = Resolve-Path (Join-Path $PSScriptRoot '..\backend')
    Push-Location $backendPath

    try {
        # Inicializa User Secrets caso ainda não exista UserSecretsId.
        & dotnet user-secrets init | Out-Null

        # Connection string do banco operacional.
        & dotnet user-secrets set 'ConnectionStrings:Business' "Host=localhost;Port=5432;Database=rotaweb_data;Username=rotaweb_data_app;Password=$dataPassword;Pooling=true;Maximum Pool Size=50"

        # Connection string de autenticação de passageiros.
        & dotnet user-secrets set 'ConnectionStrings:PassengerAuth' "Host=localhost;Port=5432;Database=rotaweb_auth;Username=rotaweb_auth_app;Password=$passengerPassword;Pooling=true;Maximum Pool Size=30"

        # Connection string de autenticação administrativa.
        & dotnet user-secrets set 'ConnectionStrings:AdminAuth' "Host=localhost;Port=5432;Database=rotaweb_admin_auth;Username=rotaweb_admin_app;Password=$adminDbPassword;Pooling=true;Maximum Pool Size=15"

        # Solicita credencial administrativa inicial sem colocar valor padrão no repositório.
        $adminEmail = Read-Host 'E-mail do primeiro administrador'
        $adminName = Read-Host 'Nome do primeiro administrador'
        $adminSecure = Read-Host 'Senha forte do primeiro administrador (mín. 14, maiúscula, minúscula, número e símbolo)' -AsSecureString
        $adminPassword = Convert-SecureStringToPlainText $adminSecure

        # Salva dados de bootstrap em User Secrets; o backend cria a conta somente se ainda não existir.
        & dotnet user-secrets set 'BootstrapAdmin:Email' $adminEmail
        & dotnet user-secrets set 'BootstrapAdmin:Name' $adminName
        & dotnet user-secrets set 'BootstrapAdmin:Password' $adminPassword

        # Mensagem final não imprime nenhuma senha.
        Write-Host ''
        Write-Host 'Bancos criados e User Secrets configurados com sucesso.' -ForegroundColor Green
        Write-Host 'Agora execute: dotnet run' -ForegroundColor Cyan
    }
    finally {
        # Volta à pasta anterior mesmo em caso de erro.
        Pop-Location
    }
}
finally {
    # Remove credencial do processo atual.
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

    # Remove SQL temporário que continha senhas técnicas aleatórias.
    Remove-Item $tempSql -Force -ErrorAction SilentlyContinue

    # Limpa referências de texto simples da memória gerenciada o quanto antes.
    $postgresPassword = $null
    $dataPassword = $null
    $passengerPassword = $null
    $adminDbPassword = $null
    $adminPassword = $null
}

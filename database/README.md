# Bancos PostgreSQL do RotaWeb

O projeto usa três bancos fisicamente separados:

- `rotaweb_data`: cidades, ônibus, viagens, passagens e auditoria.
- `rotaweb_auth`: contas e sessões de passageiros.
- `rotaweb_admin_auth`: contas e sessões administrativas.

## Preparação local recomendada

1. Instale PostgreSQL 14 ou superior.
2. Abra PowerShell na pasta `database`.
3. Execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\setup-local.ps1
```

O script exige uma instalação nova: se encontrar qualquer banco ou papel RotaWeb, interrompe antes de alterar credenciais. Solicita a senha atual de `postgres`, gera senhas técnicas aleatórias, cria os bancos e grava as conexões em **.NET User Secrets**. Não repita o setup sobre bancos já utilizados.

## Desenvolvimento

Em `appsettings.Development.json`, `Database:AutoCreateDevelopment` está ligado. Ao iniciar o backend pela primeira vez, o Entity Framework cria as tabelas dentro dos três bancos vazios.

## Produção

Em produção:

- Use HTTPS.
- Use connection strings por secret manager ou variáveis de ambiente.
- Desligue `AutoCreateDevelopment`.
- Aplique schema por pipeline de implantação/migração controlada.
- Depois do DDL, reduza as contas runtime a privilégios DML com o roteiro de `production-hardening.sql`.
- Faça backups criptografados e teste restauração periodicamente.

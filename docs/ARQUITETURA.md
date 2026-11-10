# Arquitetura do RotaWeb

## Visão geral

```text
Navegador React
      |
      | HTTPS + cookies HttpOnly + CSRF
      v
ASP.NET Core API
      |
      +-- rotaweb_data
      |    cidades, ônibus, viagens, passagens, auditoria
      |
      +-- rotaweb_auth
      |    Identity de passageiros + sessões opacas
      |
      +-- rotaweb_admin_auth
           Identity administrativo + sessões revogáveis
```

## Decisões principais

### Frontend

React organiza as páginas e mantém somente dados de exibição em memória. Credenciais de sessão não são salvas em `localStorage` ou `sessionStorage`.

### Backend

A API é a única camada autorizada a acessar PostgreSQL. O navegador nunca recebe senha de banco, connection string, hash de senha ou chave administrativa.

### Banco operacional

`rotaweb_data` mantém somente dados de negócio. `Passagens.UsuarioId` é o UUID da conta compradora; como a autenticação está em outro banco, a propriedade do registro é conferida pela API e não por FK entre bancos.

### Banco de passageiros

`rotaweb_auth` contém ASP.NET Core Identity e sessões de passageiros. O token bruto de sessão nunca é armazenado no banco; somente SHA-256 do token aleatório.

### Banco administrativo

`rotaweb_admin_auth` é separado do banco de passageiros. Após validação da senha, a API emite sessão opaca curta e revogável. O painel não pede código TOTP.

## Concorrência de assentos

A confirmação da passagem usa transação serializável, lock da viagem e índice único parcial em `(ViagemId, Assento)` quando a passagem está `Confirmada`. Assim, duas compras simultâneas não podem confirmar a mesma poltrona.

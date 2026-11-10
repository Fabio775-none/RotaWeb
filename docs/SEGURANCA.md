# Segurança do RotaWeb

## Controles implementados

- ASP.NET Core Identity para hashing de senhas e normalização de contas.
- Senha de passageiro: mínimo 10 caracteres, maiúscula, minúscula, número e 4 caracteres distintos.
- Lockout de passageiro: 5 falhas por 15 minutos.
- Senha administrativa: mínimo 14 caracteres, maiúscula, minúscula, número, símbolo e 6 caracteres distintos.
- Lockout administrativo: 4 falhas por 20 minutos.
- Login administrativo somente por e-mail e senha, sem autenticação por aplicativo.
- Bancos de autenticação separados do banco de negócio.
- Credenciais técnicas de PostgreSQL diferentes para cada banco.
- Segredos em .NET User Secrets no ambiente local; nunca em `appsettings.json`.
- Sessões opacas e revogáveis no servidor.
- Cookies `HttpOnly`, `SameSite=Strict` e, em produção, `Secure` com prefixo `__Host-`.
- Proteção CSRF com antiforgery token em POST/PUT/PATCH/DELETE.
- Rate limiting nos endpoints de autenticação.
- Mensagens genéricas de login para reduzir enumeração de contas.
- Autorização por políticas distintas: `PassengerOnly` e `AdminOnly`.
- Verificação de propriedade da passagem na query para impedir IDOR.
- Entity Framework Core e queries parametrizadas contra SQL Injection.
- CSP, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` e `Permissions-Policy`.
- Validação no frontend por UX e novamente no backend como barreira real.
- Auditoria das operações administrativas relevantes.
- Preço histórico em centavos para evitar erros de ponto flutuante.
- Transação e índice único parcial para concorrência de assentos.

## Limites honestos

Segurança absoluta não existe. A proteção depende também de HTTPS, sistema operacional atualizado, segredos fortes, backups, configuração do servidor, atualizações de dependências e monitoramento. O projeto evita promessas de “impossível de hackear” e adota defesa em profundidade.

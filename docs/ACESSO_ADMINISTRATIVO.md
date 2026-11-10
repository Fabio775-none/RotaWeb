# Acesso administrativo simples, sem 2FA

- `POST /api/admin/auth/login` valida o e-mail e a senha com ASP.NET Identity.
- Depois da senha correta, gera uma sessão opaca nova, armazenando apenas o hash no banco administrativo.
- Cookie `HttpOnly`, `SameSite=Strict` e `Secure` fora de Development.
- `GET /api/admin/auth/me` confirma a identidade e `POST /api/admin/auth/logout` revoga a sessão.
- Quatro falhas administrativas geram bloqueio por vinte minutos; há limite de 6 tentativas por 2 minutos/IP.
- Todos os endpoints administrativos usam política `AdminOnly` e antiforgery para alterações.
- Não há etapa MFA/TOTP, aplicativo autenticador, senha universal ou exceção de senha em Development.
- A coluna `TwoFactorEnabled` do Identity é preservada apenas por compatibilidade com bancos anteriores.
- O estágio `Autenticada` em `AdminSessions` foi mantido para evitar migrações.
- Cookies de pré-autenticação antigos são revogados e apagados no novo login.
- **Importante:** password-only não oferece a mesma resistência a roubo de senhas que MFA.

## Verificação

1. Abra `http://localhost:5263/admin/login`, use seu e-mail e sua senha.
2. Confirme que o painel abre diretamente, sem pedir código de aplicativo.
3. Saia e confirme que `GET /api/admin/auth/me` não aceita mais a sessão.
4. Em banco de teste, execute `Scripts/Test-AdminLogin.ps1` e, se desejar, os testes de CRUD.

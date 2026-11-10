# Evolução de engenharia e compatibilidade

## O que foi preservado

- Frontend React/Vite, backend ASP.NET Core 10 e três contextos PostgreSQL.
- Endpoints de passageiros, compras, dashboard, gestão e rotas existentes.
- Estrutura de `AdminSessions`, inclusive o valor `Autenticada`, sem migrações do banco.
- Login por senha validado pelo Identity, com lockout, CSRF, rate limiting e sessões revogáveis.
- Inicialização na porta 5263, sem encerrar processos de outras aplicações.

## Simplificação do acesso administrativo

- O login usa **somente e-mail e senha**, sem TOTP ou MFA em nenhum ambiente.
- As antigas rotas MFA do backend foram removidas; o link antigo do frontend redireciona ao login.
- Contas com MFA configurado anteriormente continuam utilizáveis com a senha normal; não são excluídas ou reiniciadas.
- Cookies legados de pré-autenticação são invalidados no novo login.
- Permanece o limite de quatro falhas por vinte minutos e seis tentativas a cada dois minutos por IP.

## Inicialização segura para bases existentes

- O processo de criação de dados de demonstração não executa `RemoveRange` de passagens ou viagens.
- Se já existirem ônibus, passagens ou viagens, não são recriados dados de demonstração.
- Contas de passageiros existentes não têm senha ou dados pessoais redefinidos a cada inicialização.
- Para redefinir uma conta administrativa existente: `BootstrapAdmin:ResetExisting=true` **somente em Development e somente por iniciativa do operador**.

## Validação e limites

- Testes Node.js do cliente HTTP: executados e aprovados nesta versão.
- Testes de contrato estático para ausência de MFA e seed não destrutivo: aprovados.
- Os testes xUnit e os builds de produção exigem SDK .NET 10 e dependências Vite instaladas. **Não foram executados neste ambiente.**
- Fluxos completos de login, compra e CRUD com PostgreSQL devem ser testados em ambiente local isolado antes de substituir uma instalação ativa.
- GitHub Actions está configurado para executar verificações automaticamente ao publicar o código.

## Aplicação

Antes de substituir uma instalação em uso, salve uma cópia dos três bancos e das chaves locais de Data Protection, mantenha os segredos em User Secrets/variáveis de ambiente e execute `Scripts/Initialize-RotaWeb.ps1`. O frontend anterior não está incluído para impedir que a tela desatualizada de autenticador reapareça.

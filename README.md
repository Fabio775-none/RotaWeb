# RotaWeb

**Sistema web de gestão e reserva demonstrativa de passagens rodoviárias.** Implementado com React, ASP.NET Core e PostgreSQL. O frontend e a API podem ser publicados na mesma origem HTTP, simplificando implantação e autenticação.

> **Escopo:** projeto funcional de demonstração. O fluxo de pagamento Pix é simulado e não realiza transação bancária.

## Público-alvo

O **RotaWeb** é direcionado a usuários e organizações envolvidos no transporte rodoviário de passageiros, oferecendo uma plataforma digital demonstrativa para consulta, reserva e gerenciamento de passagens.

O sistema contempla três perfis principais:

- **Passageiros:** pessoas que buscam praticidade na consulta de viagens rodoviárias, comparação de preços e horários, escolha de poltronas e acompanhamento de suas reservas.

- **Empresas e operadores de transporte rodoviário:** organizações que necessitam gerenciar ônibus, viagens, disponibilidade de assentos e passagens por meio de uma interface administrativa centralizada.

- **Estudantes e desenvolvedores de software:** interessados em compreender a implementação de uma aplicação web completa, incluindo arquitetura frontend e backend, autenticação, APIs REST, persistência de dados e integração com PostgreSQL.

**Finalidade do projeto:** demonstrar a aplicação de tecnologias modernas no desenvolvimento de uma plataforma de gestão e reserva de passagens rodoviárias, integrando funcionalidades voltadas ao passageiro e à administração.

O RotaWeb possui caráter demonstrativo e educacional. Nesta versão, as reservas e os pagamentos simulados não representam a comercialização efetiva de passagens.

## Funcionalidades

- Cadastro, login e perfil de passageiros.
- Consulta de viagens por origem, destino, data, categoria e preço.
- Seleção de poltrona, emissão e consulta de passagens.
- Administração de ônibus, viagens e passagens.
- Controle de assentos no PostgreSQL para evitar reservas conflitantes.
- Consulta de localidades com integração ao IBGE.
- Painel administrativo acessível por **e-mail e senha**, sem aplicativo autenticador.

## Arquitetura

| Camada | Tecnologia | Responsabilidade |
|---|---|---|
| Interface | React 19 + Vite 8 | Navegação, formulários e visualização |
| API | C# / ASP.NET Core 10 | Endpoints, regras de negócio e autorização |
| Persistência | EF Core + Npgsql | Consultas e transações PostgreSQL |
| Operacional | `rotaweb_data` | Cidades, ônibus, viagens, passagens e auditoria |
| Autenticação de passageiros | `rotaweb_auth` | Usuários, papéis e sessões |
| Autenticação administrativa | `rotaweb_admin_auth` | Administradores e sessões |
| Automação | PowerShell e GitHub Actions | Preparação, compilação, publicação e testes |

As sessões usam tokens opacos armazenados somente como hash no banco; cookies `HttpOnly`, proteção CSRF, políticas de autorização distintas, limitação de tentativas e cabeçalhos HTTP defensivos complementam o controle de acesso. O administrador usa apenas senha; **2FA não está habilitado**.

## Estrutura do código

```text
RotaWeb/
├── backend/                  API, entidades, serviços e autenticação
├── frontend/                 Aplicação React, componentes e cliente HTTP
├── database/                 Preparação de bancos e SQL de hardening
├── Scripts/                  Build, inicialização, publicação e diagnósticos
├── tests/                    Testes unitários, contratos e integração HTTP
├── .github/workflows/ci.yml  Validação contínua
├── tools/                    Verificação de arquivos sensíveis
└── README.md
```

## Requisitos

- **Windows 10/11**, PowerShell 5.1 ou 7 (scripts locais).
- **.NET SDK 10** para compilar; em máquinas de execução publicada, ASP.NET Core Runtime 10.
- **Node.js 22 LTS e npm** para compilar o frontend. Node não é necessário para executar uma publicação já compilada.
- **PostgreSQL 14 ou superior**, serviço iniciado, com o cliente `psql` e usuário administrador disponíveis.
- Git (opcional se o projeto for obtido em ZIP).

Verifique a instalação:

```powershell
dotnet --version
node --version
npm --version
psql --version
```

### 1. Baixar o projeto

```powershell
git clone https://github.com/SEU_USUARIO/RotaWeb.git
cd RotaWeb
```

Ao utilizar o ZIP, extraia a pasta `RotaWeb` e abra um PowerShell dentro dela. Substitua `SEU_USUARIO` pelo proprietário real do repositório.

### 2. Configurar PostgreSQL (máquina nova, sem bancos anteriores)

Instale o PostgreSQL e defina a senha do usuário `postgres`. Na raiz do projeto execute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\database\setup-local.ps1
```

O script localiza `psql.exe`, cria **três bancos**, gera credenciais técnicas distintas e registra as connection strings em **.NET User Secrets**. Ele solicita a conta administrativa inicial e a senha forte. Não existe senha padrão no código.

Se o PostgreSQL estiver instalado em local não convencional:

```powershell
.\database\setup-local.ps1 -PsqlPath 'D:\PostgreSQL\bin\psql.exe'
```

**Proteção de dados:** esse instalador **recusa executar se encontrar bancos ou papéis RotaWeb existentes**. Não o execute para atualizar uma instalação em uso. Para reutilizar uma instalação, preserve os bancos e configure as três conexões já existentes com `dotnet user-secrets`, conforme a próxima seção.

### 3. Compilar e iniciar

```powershell
.\Scripts\Initialize-RotaWeb.ps1
```

O comando executa `npm ci`, os testes JavaScript, o build otimizado do React, integra a interface em `backend/wwwroot`, inicia a API e verifica rotas públicas. Na primeira execução em um banco vazio, o backend pode criar os dados demonstrativos configurados. Não apaga registros existentes. A compilação mantém a interface anterior se um build falhar.

**Aplicação:** `http://localhost:5263`  
**Administração:** `http://localhost:5263/admin/login`

Para executar novamente sem recompilar:

```powershell
.\Scripts\Start-RotaWeb.ps1
```

Para gerar somente o frontend integrado, sem iniciar a aplicação:

```powershell
.\Scripts\Build-RotaWeb.ps1
```

## Compilação manual

Use os comandos abaixo a partir da raiz do repositório:

```powershell
cd frontend
npm ci
npm test
npm run build
cd ..
dotnet restore backend/backend.csproj
dotnet build backend/backend.csproj -c Release
dotnet test tests/RotaWeb.Tests/RotaWeb.Tests.csproj -c Release
```

O `npm run build` gera `frontend/dist`. Para hospedar React dentro do ASP.NET Core, execute `Scripts/Build-RotaWeb.ps1`, que copia o build validado para `backend/wwwroot`.

Para gerar uma publicação integrada:

```powershell
.\Scripts\Publish-RotaWeb.ps1
```

A saída é `publish/`. O comando **não cria nem modifica bancos**. A publicação anterior é preservada caso a compilação falhe.

## Executar em outro computador

**Instalação nova:** instale .NET SDK 10, Node.js 22 e PostgreSQL; copie ou clone o repositório; execute `database/setup-local.ps1` e depois `Scripts/Initialize-RotaWeb.ps1`. Cada máquina mantém suas próprias credenciais locais.

**Transferência com dados existentes:** não copie apenas o código. Faça backup individual dos três bancos (`pg_dump -Fc`), salve separadamente as connection strings e, se desejar preservar sessões ativas, as chaves de Data Protection em `backend/.keys`. No destino, instale o PostgreSQL, prepare os bancos e restaure os três backups com `pg_restore` sob os papéis correspondentes. Configure as novas connection strings **antes de iniciar a API**. Mantenha os backups fora do repositório; não execute a instalação inicial sobre bancos restaurados.

Exemplo de backup, com `pg_dump` disponível no PATH (solicitará autenticação conforme sua instalação):

```powershell
$backupDir = Join-Path $env:USERPROFILE 'RotaWeb-Backups'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
pg_dump -h localhost -U postgres -Fc -f (Join-Path $backupDir 'rotaweb_data.backup') rotaweb_data
pg_dump -h localhost -U postgres -Fc -f (Join-Path $backupDir 'rotaweb_auth.backup') rotaweb_auth
pg_dump -h localhost -U postgres -Fc -f (Join-Path $backupDir 'rotaweb_admin_auth.backup') rotaweb_admin_auth
```

A restauração deve ser realizada em um ambiente de destino de teste, verificando proprietários/permissões dos objetos antes de colocá-lo em uso. A migração não deve sobrescrever bancos de produção sem backup validado. Perder as chaves de Data Protection pode invalidar cookies existentes; **não apaga as contas nem as passagens**.

### Conexões com os três bancos

Em desenvolvimento, no diretório `backend`:

```powershell
cd backend
dotnet user-secrets set 'ConnectionStrings:Business' 'Host=localhost;Port=5432;Database=rotaweb_data;Username=USUARIO;Password=SENHA'
dotnet user-secrets set 'ConnectionStrings:PassengerAuth' 'Host=localhost;Port=5432;Database=rotaweb_auth;Username=USUARIO;Password=SENHA'
dotnet user-secrets set 'ConnectionStrings:AdminAuth' 'Host=localhost;Port=5432;Database=rotaweb_admin_auth;Username=USUARIO;Password=SENHA'
cd ..
```

Substitua os valores de exemplo pelas credenciais reais, distintas para cada banco. Em produção, use um secret manager ou as variáveis de ambiente `ConnectionStrings__Business`, `ConnectionStrings__PassengerAuth` e `ConnectionStrings__AdminAuth`. **Não publique secrets, senhas ou backups no GitHub.**

Para cadastrar o primeiro administrador em um ambiente novo, configure `BootstrapAdmin:Email`, `BootstrapAdmin:Password` (mínimo de 14 caracteres e complexidade exigida) e, opcionalmente, `BootstrapAdmin:Name` nos User Secrets. O sistema cria a conta se ela ainda não existir; não substitui automaticamente a senha de uma conta existente. A recuperação local autorizada está descrita em `docs/ACESSO_ADMINISTRATIVO.md`.

## Qualidade e verificações

| Verificação | Comando |
|---|---|
| Testes do cliente React | `cd frontend; npm test` |
| Testes unitários .NET | `dotnet test tests/RotaWeb.Tests/RotaWeb.Tests.csproj` |
| Revisão de arquivos sensíveis | `node tools/check-repository.mjs` |
| Contratos críticos de manutenção | `node --test tests/source-contracts/*.mjs` |
| API e PostgreSQL em execução | `.\Scripts\Test-Integration.ps1` |
| Autenticação administrativa | `.\Scripts\Test-AdminLogin.ps1` |
| CRUD administrativo | `.\Scripts\Test-AdminCrud.ps1` **somente em banco de teste** |

O GitHub Actions realiza build e testes independentes e prepara bancos **descartáveis** para testar HTTP, CSRF, login, sessão administrativa e revogação. Os testes de integração não devem rodar contra bases reais.

Para exigir dados de demonstração nos testes integrados, execute `.\Scripts\Test-Integration.ps1 -RequireDemoData`. O teste padrão funciona também com banco válido sem viagens cadastradas.

Health: `GET /api/security/health` verifica o processo. Readiness: `GET /api/security/ready` verifica **as três conexões PostgreSQL**.

## Publicar no GitHub

```powershell
git init
git add .
git status
git commit -m 'Initial commit: RotaWeb'
git branch -M main
git remote add origin https://github.com/SEU_USUARIO/RotaWeb.git
git push -u origin main
```

Revise o `git status` antes do commit. O `.gitignore` exclui dependências, builds, chaves, arquivos de ambiente e publicações locais. Em um repositório já iniciado, não repita `git init` nem `git remote add origin`.

## Limites de implantação

A aplicação deve ser hospedada por HTTPS em produção, com PostgreSQL, backups verificados, secrets externos, chaves de Data Protection persistidas e permissões mínimas. Não exponha os scripts de demonstração, a configuração de desenvolvimento ou os bancos PostgreSQL à Internet. A prontidão para produção depende de testes funcionais, revisão de segurança e da infraestrutura do operador.

**Licença:** não definida neste repositório. Adicione um arquivo `LICENSE` somente quando decidir os termos de distribuição.

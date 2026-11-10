using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Security;

namespace RotaWeb.Api.Services;

/// <summary>
/// Inicializa os três bancos em desenvolvimento e cria dados completos de demonstração.
/// Em produção, AutoCreateDevelopment e SeedDemoData devem permanecer desligados.
/// </summary>
public sealed class DatabaseBootstrapper
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DatabaseBootstrapper(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _services = services;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    /// Garante schema, papéis, contas de demonstração e dados de negócio.
    /// </summary>
    public async Task RunAsync()
    {
        using var scope = _services.CreateScope();

        var passengerDb = scope.ServiceProvider.GetRequiredService<PassengerAuthDbContext>();
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminAuthDbContext>();
        var businessDb = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();

        if (_environment.IsDevelopment() && _configuration.GetValue("Database:AutoCreateDevelopment", false))
        {
            await passengerDb.Database.EnsureCreatedAsync();
            await adminDb.Database.EnsureCreatedAsync();
            await businessDb.Database.EnsureCreatedAsync();
        }

        // Compatibilidade com os bancos já criados pelas versões anteriores do projeto.
        // EnsureCreated não altera tabelas existentes, então adicionamos apenas as colunas novas de forma idempotente.
        await EnsureAgeAndGuardianColumnsAsync(passengerDb, businessDb);

        await EnsureRolesAsync(scope.ServiceProvider);
        await EnsureBootstrapAdminAsync(scope.ServiceProvider);

        Guid? demoPassengerId = null;
        if (_environment.IsDevelopment() && _configuration.GetValue("Database:SeedDemoUsers", false))
        {
            demoPassengerId = await EnsureDemoPassengerAsync(scope.ServiceProvider);
        }

        if (_environment.IsDevelopment() && _configuration.GetValue("Database:SeedDemoData", false))
        {
            await EnsureDemoBusinessDataAsync(businessDb, demoPassengerId);
        }
    }


    /// <summary>
    /// Adiciona de forma idempotente as colunas introduzidas pelas regras de maioridade e responsável.
    /// Isso permite atualizar uma instalação V7/V8 sem apagar dados existentes.
    /// </summary>
    private static async Task EnsureAgeAndGuardianColumnsAsync(PassengerAuthDbContext passengerDb, BusinessDbContext businessDb)
    {
        await passengerDb.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"AspNetUsers\" ADD COLUMN IF NOT EXISTS \"DataNascimento\" date NULL;");

        await businessDb.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Passagens" ALTER COLUMN "Documento" TYPE varchar(40);
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "TipoDocumento" varchar(30) NULL;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "DataNascimento" date NULL;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "ResponsavelNome" varchar(100) NULL;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "ResponsavelDocumento" varchar(40) NULL;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "ResponsavelDataNascimento" date NULL;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "ResponsavelVinculo" varchar(40) NULL;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "ResponsavelAcompanha" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Passagens" ADD COLUMN IF NOT EXISTS "DocumentacaoMenorConfirmada" boolean NOT NULL DEFAULT false;
            """);
    }

    private static async Task EnsureRolesAsync(IServiceProvider services)
    {
        var passengerRoles = services.GetRequiredService<RoleManager<PassengerRole>>();
        if (!await passengerRoles.RoleExistsAsync("Passageiro"))
        {
            var result = await passengerRoles.CreateAsync(new PassengerRole { Name = "Passageiro" });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Não foi possível criar o papel Passageiro.");
            }
        }

        var adminRoles = services.GetRequiredService<RoleManager<AdminRole>>();
        if (!await adminRoles.RoleExistsAsync("Administrador"))
        {
            var result = await adminRoles.CreateAsync(new AdminRole { Name = "Administrador" });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Não foi possível criar o papel Administrador.");
            }
        }
    }

    /// <summary>
    /// Cria ou, somente em Development quando autorizado, redefine a conta administrativa da apresentação.
    /// </summary>
    private async Task EnsureBootstrapAdminAsync(IServiceProvider services)
    {
        // Bootstrap opt-in: somente segredos locais/variáveis de ambiente, nunca credenciais fixas.
        // Uma conta existente não é redefinida sem ResetExisting em Development.
        var email = _configuration["BootstrapAdmin:Email"];
        var password = _configuration["BootstrapAdmin:Password"];
        var name = _configuration["BootstrapAdmin:Name"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (!AdminPasswordPolicy.IsValid(password, out var passwordError))
        {
            throw new InvalidOperationException($"Senha do administrador inicial inválida: {passwordError}");
        }

        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var existing = await userManager.FindByEmailAsync(email.Trim());

        if (existing is null)
        {
            existing = new AdminUser
            {
                Id = Guid.NewGuid(),
                Nome = name.Trim(),
                Email = email.Trim(),
                UserName = email.Trim(),
                EmailConfirmed = true,
                LockoutEnabled = true,
                CriadoEm = DateTime.UtcNow
            };

            var create = await userManager.CreateAsync(existing, password);
            if (!create.Succeeded)
            {
                var errors = string.Join("; ", create.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Falha ao criar administrador inicial: {errors}");
            }
        }
        else if (_environment.IsDevelopment() && _configuration.GetValue("BootstrapAdmin:ResetExisting", false))
        {
            existing.Nome = name.Trim();
            existing.EmailConfirmed = true;
            existing.LockoutEnd = null;
            existing.AccessFailedCount = 0;
            await userManager.UpdateAsync(existing);

            // Redefinição explícita de senha em Development, sem alterar demais dados
            // da conta nem revogar automaticamente as configurações legadas.
            var token = await userManager.GeneratePasswordResetTokenAsync(existing);
            var reset = await userManager.ResetPasswordAsync(existing, token, password);
            if (!reset.Succeeded)
            {
                var errors = string.Join("; ", reset.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Falha ao redefinir a senha administrativa de demonstração: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(existing, "Administrador"))
        {
            await userManager.AddToRoleAsync(existing, "Administrador");
        }
    }

    /// <summary>
    /// Garante uma conta de passageiro conhecida para a apresentação local.
    /// A senha continua armazenada apenas como hash pelo ASP.NET Core Identity.
    /// </summary>
    private async Task<Guid?> EnsureDemoPassengerAsync(IServiceProvider services)
    {
        var email = _configuration["DemoPassenger:Email"];
        var password = _configuration["DemoPassenger:Password"];
        var name = _configuration["DemoPassenger:Name"] ?? "Passageiro Demo";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var userManager = services.GetRequiredService<UserManager<PassengerUser>>();
        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null)
        {
            user = new PassengerUser
            {
                Id = Guid.NewGuid(),
                Nome = name.Trim(),
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true,
                LockoutEnabled = true,
                DataNascimento = new DateOnly(1990, 1, 1),
                CriadoEm = DateTime.UtcNow
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                var errors = string.Join("; ", create.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Falha ao criar passageiro de demonstração: {errors}");
            }
        }
        // A conta já existente é preservada integralmente durante a inicialização.

        if (!await userManager.IsInRoleAsync(user, "Passageiro"))
        {
            await userManager.AddToRoleAsync(user, "Passageiro");
        }

        return user.Id;
    }

    /// <summary>
    /// Prepara uma base de demonstração estável para a apresentação.
    /// A preparação pesada roda uma única vez por banco (marcador V8_5_ADMIN_CRUD_READY),
    /// para que alterações feitas pelo administrador não sejam recriadas no próximo início.
    /// </summary>
    private static async Task EnsureDemoBusinessDataAsync(BusinessDbContext db, Guid? demoPassengerId)
    {
        const string seedMarker = "V8_5_ADMIN_CRUD_READY";

        // Depois da primeira preparação, nunca volta a sobrescrever/excluir alterações do administrador.
        if (await db.AuditEvents.AsNoTracking().AnyAsync(item => item.Acao == seedMarker && item.Entidade == "Sistema"))
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        // Nunca execute limpeza de passagens/viagens na inicialização.
        // Base já usada deve permanecer intocada, inclusive quando não há marcador de seed.
        if (await db.Passagens.AnyAsync() || await db.Viagens.AnyAsync() || await db.Onibus.AnyAsync())
        {
            await transaction.RollbackAsync();
            return;
        }

        var demoPrefixes = new[] { "RW-101", "RW-102", "RW-103", "RW-104", "RW-201", "RW-202", "RW-203", "RW-204" };

        var cities = new[]
        {
            new Cidade { Id = 3550308, Nome = "São Paulo", Uf = "SP" },
            new Cidade { Id = 3304557, Nome = "Rio de Janeiro", Uf = "RJ" },
            new Cidade { Id = 3106200, Nome = "Belo Horizonte", Uf = "MG" },
            new Cidade { Id = 4106902, Nome = "Curitiba", Uf = "PR" },
            new Cidade { Id = 3509502, Nome = "Campinas", Uf = "SP" },
            new Cidade { Id = 3303302, Nome = "Niterói", Uf = "RJ" },
            new Cidade { Id = 4113700, Nome = "Londrina", Uf = "PR" },
            new Cidade { Id = 3170206, Nome = "Uberlândia", Uf = "MG" }
        };

        foreach (var city in cities)
        {
            if (!await db.Cidades.AnyAsync(item => item.Id == city.Id))
            {
                db.Cidades.Add(city);
            }
        }
        await db.SaveChangesAsync();

        // Sete ônibus têm programação de demonstração e o RW-204 fica totalmente livre
        // para o professor/aluno demonstrar cadastro e edição de viagens sem conflito de agenda.
        var busDefinitions = new[]
        {
            new Onibus { Prefixo = "RW-101", Placa = "ABC1D23", Modelo = "Marcopolo Paradiso", Categoria = "Executivo", Assentos = 36, Ativo = true },
            new Onibus { Prefixo = "RW-102", Placa = "DEF4G56", Modelo = "Comil Campione", Categoria = "Semi-leito", Assentos = 32, Ativo = true },
            new Onibus { Prefixo = "RW-103", Placa = "GHI7J89", Modelo = "Busscar Vissta Buss", Categoria = "Leito", Assentos = 24, Ativo = true },
            new Onibus { Prefixo = "RW-104", Placa = "JKL1M23", Modelo = "Marcopolo Viaggio", Categoria = "Convencional", Assentos = 40, Ativo = true },
            new Onibus { Prefixo = "RW-201", Placa = "MNO4P56", Modelo = "Marcopolo Senior", Categoria = "Executivo", Assentos = 42, Ativo = true },
            new Onibus { Prefixo = "RW-202", Placa = "QRS7T89", Modelo = "Comil Invictus", Categoria = "Executivo", Assentos = 42, Ativo = true },
            new Onibus { Prefixo = "RW-203", Placa = "UVW1X23", Modelo = "Busscar El Buss", Categoria = "Convencional", Assentos = 44, Ativo = true },
            new Onibus { Prefixo = "RW-204", Placa = "YZA4B56", Modelo = "Marcopolo Paradiso G7", Categoria = "Leito", Assentos = 28, Ativo = true }
        };

        db.Onibus.AddRange(busDefinitions);
        await db.SaveChangesAsync();

        var busList = await db.Onibus
            .Where(item => demoPrefixes.Contains(item.Prefixo))
            .OrderBy(item => item.Prefixo)
            .ToListAsync();
        var buses = busList.ToDictionary(item => item.Prefixo, StringComparer.OrdinalIgnoreCase);

        void AddTrip(string prefix, int origemId, int destinoId, DateTime partida, int durationHours, int price)
        {
            var bus = buses[prefix];
            db.Viagens.Add(new Viagem
            {
                OnibusId = bus.Id,
                OrigemId = origemId,
                DestinoId = destinoId,
                Partida = partida,
                Chegada = partida.AddHours(durationHours),
                PrecoCentavos = price,
                Assentos = bus.Assentos,
                Status = "Programada"
            });
        }

        // Agenda sem sobreposição: cada ônibus tem horários que não colidem com os próprios serviços.
        // Todas as oito cidades aparecem em viagens reais e São Paulo -> Rio mantém quatro opções diárias.
        for (var day = 1; day <= 30; day++)
        {
            var d = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(day), DateTimeKind.Utc);

            AddTrip("RW-104", 3550308, 3304557, d.AddHours(10), 6, 9990);   // Convencional
            AddTrip("RW-101", 3550308, 3304557, d.AddHours(14), 6, 12990);  // Executivo
            AddTrip("RW-102", 3550308, 3304557, d.AddHours(20), 6, 16490);  // Semi-leito
            AddTrip("RW-103", 3550308, 3304557, d.AddHours(23), 6, 20990);  // Leito

            AddTrip("RW-201", 3106200, 3170206, d.AddHours(7), 6, 11990);   // BH -> Uberlândia
            AddTrip("RW-201", 3170206, 3106200, d.AddHours(15), 6, 11990);  // Uberlândia -> BH

            AddTrip("RW-202", 4106902, 4113700, d.AddHours(7), 6, 10990);   // Curitiba -> Londrina
            AddTrip("RW-202", 4113700, 4106902, d.AddHours(15), 6, 10990);  // Londrina -> Curitiba

            AddTrip("RW-203", 3509502, 3303302, d.AddHours(7), 7, 12490);   // Campinas -> Niterói
            AddTrip("RW-203", 3303302, 3509502, d.AddHours(16), 7, 12490);  // Niterói -> Campinas
        }

        await db.SaveChangesAsync();

        // Deixa uma passagem pronta para a demonstração de "Minhas passagens".
        if (demoPassengerId.HasValue)
        {
            var firstTrip = await db.Viagens
                .AsNoTracking()
                .Where(item => item.OrigemId == 3550308 && item.DestinoId == 3304557 && item.Partida > DateTime.UtcNow)
                .OrderBy(item => item.Partida)
                .FirstOrDefaultAsync();

            if (firstTrip is not null)
            {
                db.Passagens.Add(new Passagem
                {
                    Id = Guid.NewGuid(),
                    Codigo = $"RW{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                    ViagemId = firstTrip.Id,
                    UsuarioId = demoPassengerId.Value,
                    Assento = 7,
                    Passageiro = "Passageiro Demo",
                    Documento = "12345678900",
                    TipoDocumento = "RG/CIN",
                    DataNascimento = new DateOnly(1990, 1, 1),
                    ValorCentavos = firstTrip.PrecoCentavos,
                    Status = "Confirmada",
                    CompradoEm = DateTime.UtcNow
                });
            }
        }

        // Marcador persistente: nas próximas inicializações o seed não toca novamente nos dados.
        db.AuditEvents.Add(new AuditEvent
        {
            AdminId = Guid.Empty,
            Acao = seedMarker,
            Entidade = "Sistema",
            EntidadeId = "V8.5",
            Detalhes = "Base de demonstração preparada para CRUD administrativo sem sobreposição.",
            CriadoEm = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}

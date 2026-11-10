using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Security;
using RotaWeb.Api.Services;

// Cria o builder padrão do ASP.NET Core e carrega appsettings, variáveis de ambiente e User Secrets em Development.
var builder = WebApplication.CreateBuilder(args);

// Função local que exige uma connection string sem expor seu conteúdo em logs ou mensagens de erro.
static string RequireConnectionString(IConfiguration configuration, string name) =>
    configuration.GetConnectionString(name)
    ?? throw new InvalidOperationException($"A connection string '{name}' não foi configurada por User Secrets ou variável de ambiente.");

// Lê as três conexões fisicamente separadas definidas pelo projeto de segurança.
var businessConnection = RequireConnectionString(builder.Configuration, "Business");
var passengerAuthConnection = RequireConnectionString(builder.Configuration, "PassengerAuth");
var adminAuthConnection = RequireConnectionString(builder.Configuration, "AdminAuth");

// Registra o banco operacional de viagens, ônibus e passagens.
builder.Services.AddDbContext<BusinessDbContext>(options =>
    options.UseNpgsql(businessConnection));

// Registra o banco exclusivo de autenticação de passageiros.
builder.Services.AddDbContext<PassengerAuthDbContext>(options =>
    options.UseNpgsql(passengerAuthConnection));

// Registra o banco exclusivo de autenticação administrativa.
builder.Services.AddDbContext<AdminAuthDbContext>(options =>
    options.UseNpgsql(adminAuthConnection));

// Configura Identity para passageiros com política de senha definida na documentação acadêmica.
builder.Services
    .AddIdentityCore<PassengerUser>(options =>
    {
        // Senhas precisam de no mínimo dez caracteres.
        options.Password.RequiredLength = 10;
        // Pelo menos quatro caracteres distintos reduzem senhas triviais repetidas.
        options.Password.RequiredUniqueChars = 4;
        // A política exige maiúscula.
        options.Password.RequireUppercase = true;
        // A política exige minúscula.
        options.Password.RequireLowercase = true;
        // A política exige número.
        options.Password.RequireDigit = true;
        // A documentação não exige caractere especial para passageiro.
        options.Password.RequireNonAlphanumeric = false;
        // Cinco falhas consecutivas provocam bloqueio temporário.
        options.Lockout.MaxFailedAccessAttempts = 5;
        // O bloqueio dura quinze minutos.
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        // Contas novas já podem ser bloqueadas contra força bruta.
        options.Lockout.AllowedForNewUsers = true;
        // E-mail único é obrigatório.
        options.User.RequireUniqueEmail = true;
    })
    // Usa tipo de papel exclusivo do banco de passageiros.
    .AddRoles<PassengerRole>()
    // Persiste usuários e papéis no contexto rotaweb_auth.
    .AddEntityFrameworkStores<PassengerAuthDbContext>()
    // Registra provedores de token do Identity.
    .AddDefaultTokenProviders();

// Registra o Identity administrativo em banco separado.
// A configuração global do Identity permanece com a política do passageiro, pois IdentityOptions não é genérico.
// Regras administrativas adicionais (senha de 14 caracteres, símbolo e lockout em 4 falhas) são aplicadas explicitamente
// pelo bootstrap e pelo controller administrativo para evitar que uma configuração contamine a outra.
builder.Services
    .AddIdentityCore<AdminUser>()
    // Papel próprio evita colisão de tipos com o banco de passageiros.
    .AddRoles<AdminRole>()
    // Persiste a conta no banco rotaweb_admin_auth.
    .AddEntityFrameworkStores<AdminAuthDbContext>()
    // Mantém provedores de token do Identity para recuperação administrativa autorizada.
    .AddDefaultTokenProviders();

// Registra autenticação com sessão opaca de passageiro como esquema padrão.
builder.Services
    .AddAuthentication(options =>
    {
        // Requisições comuns protegidas usam passageiro por padrão.
        options.DefaultAuthenticateScheme = AuthSchemes.Passenger;
        // Desafio padrão também é de passageiro; endpoints admin informam seu esquema explicitamente.
        options.DefaultChallengeScheme = AuthSchemes.Passenger;
    })
    // Handler consulta o hash do token no banco de passageiros.
    .AddScheme<AuthenticationSchemeOptions, PassengerSessionAuthenticationHandler>(AuthSchemes.Passenger, _ => { })
    // Handler administrativo valida sessão criada após a senha correta.
    .AddScheme<AuthenticationSchemeOptions, AdminSessionAuthenticationHandler>(AuthSchemes.Admin, _ => { });

// Define políticas explícitas para facilitar leitura e auditoria dos endpoints.
builder.Services.AddAuthorization(options =>
{
    // Política de passageiro exige o esquema e papel corretos.
    options.AddPolicy("PassengerOnly", policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Passenger);
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Passageiro");
    });

    // Política administrativa nunca aceita cookie de passageiro.
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Admin);
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Administrador");
    });
});

// Persistência local das chaves do Data Protection evita perda de tokens antiforgery entre reinicializações.
builder.Services
    .AddDataProtection()
    .SetApplicationName("RotaWeb")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));

// Calcula o nome seguro do cookie antiforgery de acordo com HTTP local ou HTTPS de produção.
var antiforgeryCookieName = builder.Environment.IsDevelopment()
    ? "RotaWeb.Antiforgery"
    : "__Host-RotaWeb.Antiforgery";

// Configura proteção CSRF para qualquer operação que altera estado.
builder.Services.AddAntiforgery(options =>
{
    // Frontend recebe o token de requisição por endpoint e o envia neste cabeçalho.
    options.HeaderName = "X-CSRF-TOKEN";
    // Cookie interno não precisa ficar acessível a JavaScript.
    options.Cookie.HttpOnly = true;
    // Nome __Host- em produção impede Domain e exige Path=/.
    options.Cookie.Name = antiforgeryCookieName;
    // Produção exige HTTPS; desenvolvimento local aceita HTTP.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    // Strict bloqueia envio cross-site do cookie antiforgery.
    options.Cookie.SameSite = SameSiteMode.Strict;
    // __Host- exige raiz.
    options.Cookie.Path = "/";
});

// Rate limiting é particionado pelo IP remoto para reduzir força bruta sem bloquear todos os visitantes juntos.
builder.Services.AddRateLimiter(options =>
{
    // Resposta padrão quando um limite é excedido.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Limite para cadastro e login de passageiro.
    options.AddPolicy<string>("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Limite administrativo deliberadamente mais restritivo.
    options.AddPolicy<string>("admin-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(2),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// CORS fica restrito às origens explicitamente configuradas; em produção o frontend recomendado é same-origin.
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// Registra controllers e resposta padronizada para exceções inesperadas.
builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();

// Cache em memória é usado somente para localidades públicas do IBGE.
builder.Services.AddMemoryCache();

// Cliente do IBGE usa base oficial e timeout curto para não travar a API.
builder.Services.AddHttpClient<IbgeService>(client =>
{
    client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/api/v1/localidades/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RotaWeb/1.0");
});

// Serviços criptográficos e de sessão são stateless ou scoped conforme acesso ao banco.
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddSingleton<CookieNameService>();
builder.Services.AddScoped<PassengerSessionService>();
builder.Services.AddScoped<AdminSessionService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddSingleton<DatabaseBootstrapper>();

// Constrói a aplicação depois de concluir todos os registros de serviço.
var app = builder.Build();

// Erros inesperados retornam ProblemDetails sem stack trace para o cliente.
app.UseExceptionHandler();

// Produção exige HSTS e redirecionamento para HTTPS.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Adiciona CSP e outros cabeçalhos defensivos antes de servir conteúdo.
app.UseMiddleware<SecurityHeadersMiddleware>();

// Em desenvolvimento permite somente as origens definidas em configuração.
if (allowedOrigins.Length > 0)
{
    app.UseCors("Frontend");
}

// Ativa rate limiting antes dos endpoints.
app.UseRateLimiter();

// Lê e valida as sessões opacas quando um endpoint exige identidade.
app.UseAuthentication();

// Aplica as políticas PassengerOnly e AdminOnly.
app.UseAuthorization();

// Serve o build React quando ele tiver sido copiado para wwwroot pelo script de publicação.
app.UseDefaultFiles();
app.UseStaticFiles();

// Expõe todos os controllers /api.
app.MapControllers();

// Rotas não pertencentes à API retornam o index do React em publicação integrada.
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.MapFallbackToFile("index.html");
}

// Prepara tabelas em Development e seeds mínimos sem incluir senha fixa no projeto.
await app.Services.GetRequiredService<DatabaseBootstrapper>().RunAsync();

// Inicia o servidor HTTP.
app.Run();

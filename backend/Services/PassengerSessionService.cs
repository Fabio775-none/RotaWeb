using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Security;

namespace RotaWeb.Api.Services;

/// <summary>
/// Emite, revoga e limpa sessões de passageiros.
/// A sessão fica revogável no servidor e o cookie nunca contém dados pessoais ou um JWT reutilizável fora do sistema.
/// </summary>
public sealed class PassengerSessionService
{
    /// <summary>
    /// Banco de autenticação de passageiros.
    /// </summary>
    private readonly PassengerAuthDbContext _db;

    /// <summary>
    /// Serviço criptográfico de tokens.
    /// </summary>
    private readonly SessionTokenService _tokens;

    /// <summary>
    /// Serviço que resolve o nome seguro do cookie.
    /// </summary>
    private readonly CookieNameService _cookies;

    /// <summary>
    /// Configurações de duração de sessão.
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Ambiente atual, usado para SecurePolicy no desenvolvimento local.
    /// </summary>
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Inicializa todas as dependências necessárias.
    /// </summary>
    public PassengerSessionService(
        PassengerAuthDbContext db,
        SessionTokenService tokens,
        CookieNameService cookies,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _db = db;
        _tokens = tokens;
        _cookies = cookies;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    /// Cria uma sessão, persiste somente seu hash e grava o token bruto em cookie HttpOnly.
    /// </summary>
    public async Task IssueAsync(HttpContext httpContext, PassengerUser user, bool persistent)
    {
        // Gera token aleatório que será conhecido somente pelo navegador.
        var rawToken = _tokens.CreateToken();

        // Define expiração curta por padrão e prazo maior somente quando o usuário solicita persistência.
        var minutes = _configuration.GetValue("Security:PassengerSessionMinutes", 30);
        var days = _configuration.GetValue("Security:PassengerRememberDays", 7);
        var expires = persistent ? DateTime.UtcNow.AddDays(days) : DateTime.UtcNow.AddMinutes(minutes);

        // Persiste o hash para que o banco não contenha credencial de sessão utilizável diretamente.
        var session = new PassengerSession
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(rawToken),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = expires,
            Persistente = persistent,
            UserAgentHash = _tokens.HashUserAgent(httpContext.Request.Headers["User-Agent"].ToString())
        };

        // Adiciona a nova sessão ao banco dedicado.
        _db.Sessions.Add(session);

        // Persiste antes de responder para garantir que o cookie sempre corresponda a uma sessão conhecida pelo servidor.
        await _db.SaveChangesAsync();

        // Configura cookie contra acesso por JavaScript e uso cross-site.
        httpContext.Response.Cookies.Append(
            _cookies.PassengerSession,
            rawToken,
            BuildCookieOptions(expires, persistent));
    }

    /// <summary>
    /// Revoga a sessão correspondente ao cookie atual e remove o cookie do navegador.
    /// </summary>
    public async Task RevokeCurrentAsync(HttpContext httpContext)
    {
        // Lê o cookie somente no servidor.
        if (httpContext.Request.Cookies.TryGetValue(_cookies.PassengerSession, out var rawToken) &&
            !string.IsNullOrWhiteSpace(rawToken))
        {
            // Localiza a sessão por hash sem comparar token bruto no banco.
            var hash = _tokens.HashToken(rawToken);
            var session = await _db.Sessions.FirstOrDefaultAsync(item => item.TokenHash == hash);

            // Marca revogação sem apagar o registro, preservando trilha técnica mínima.
            if (session is not null && session.RevogadoEm is null)
            {
                session.RevogadoEm = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        // Remove o cookie mesmo quando o registro já não existe.
        httpContext.Response.Cookies.Delete(_cookies.PassengerSession, BuildDeleteCookieOptions());
    }

    /// <summary>
    /// Revoga todas as sessões de uma conta, usado após mudança de senha ou resposta a incidente.
    /// </summary>
    public async Task RevokeAllAsync(Guid userId)
    {
        // Busca somente sessões ainda ativas para reduzir escrita desnecessária.
        var sessions = await _db.Sessions
            .Where(item => item.UserId == userId && item.RevogadoEm == null && item.ExpiraEm > DateTime.UtcNow)
            .ToListAsync();

        // Aplica o mesmo instante de revogação a todas.
        var now = DateTime.UtcNow;
        foreach (var session in sessions)
        {
            session.RevogadoEm = now;
        }

        // Persiste em uma única operação.
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Constrói opções seguras para o cookie de sessão.
    /// </summary>
    private CookieOptions BuildCookieOptions(DateTime expiresUtc, bool persistent)
    {
        // Em produção Secure é obrigatório; em desenvolvimento local HTTP é aceito para facilitar execução no computador do aluno.
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = persistent ? new DateTimeOffset(expiresUtc) : null,
            MaxAge = persistent ? expiresUtc - DateTime.UtcNow : null
        };
    }

    /// <summary>
    /// Constrói opções equivalentes para exclusão consistente do cookie.
    /// </summary>
    private CookieOptions BuildDeleteCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };
    }
}

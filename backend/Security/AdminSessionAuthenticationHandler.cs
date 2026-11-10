using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RotaWeb.Api.Data;

namespace RotaWeb.Api.Security;

/// <summary>
/// Autentica sessões administrativas válidas após login com senha.
/// Sessões temporárias legadas não são autorizadas.
/// </summary>
public sealed class AdminSessionAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Banco administrativo isolado.
    /// </summary>
    private readonly AdminAuthDbContext _db;

    /// <summary>
    /// Serviço de hash dos tokens.
    /// </summary>
    private readonly SessionTokenService _tokens;

    /// <summary>
    /// Nomes dos cookies do ambiente.
    /// </summary>
    private readonly CookieNameService _cookies;

    /// <summary>
    /// Inicializa o handler.
    /// </summary>
    public AdminSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AdminAuthDbContext db,
        SessionTokenService tokens,
        CookieNameService cookies)
        : base(options, logger, encoder)
    {
        _db = db;
        _tokens = tokens;
        _cookies = cookies;
    }

    /// <summary>
    /// Valida token administrativo, expiração, revogação e SecurityStamp.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Sem cookie administrativo a requisição não está autenticada como administrador.
        if (!Request.Cookies.TryGetValue(_cookies.AdminSession, out var rawToken) ||
            string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticateResult.NoResult();
        }

        // Consulta a sessão usando somente o hash.
        var hash = _tokens.HashToken(rawToken);
        var session = await _db.Sessions
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.TokenHash == hash);

        // Exige sessão ativa e explicitamente marcada como autenticada.
        if (session is null ||
            session.Estagio != "Autenticada" ||
            session.RevogadoEm is not null ||
            session.ExpiraEm <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Sessão administrativa inválida.");
        }

        // Qualquer mudança de segurança invalida a sessão anterior.
        if (session.SecurityStamp != (session.User.SecurityStamp ?? string.Empty))
        {
            return AuthenticateResult.Fail("Sessão administrativa invalidada.");
        }

        // Cria claims mínimas e com papel explícito de Administrador.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
            new(ClaimTypes.Name, session.User.Nome),
            new(ClaimTypes.Email, session.User.Email ?? string.Empty),
            new(ClaimTypes.Role, "Administrador")
        };

        // Constrói ticket reconhecido pelo middleware de autorização.
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        // Libera a requisição como autenticada.
        return AuthenticateResult.Success(ticket);
    }
}

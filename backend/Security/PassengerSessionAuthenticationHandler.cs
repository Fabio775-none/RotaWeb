using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RotaWeb.Api.Data;

namespace RotaWeb.Api.Security;

/// <summary>
/// Autentica requisições de passageiro usando uma sessão opaca revogável armazenada no banco rotaweb_auth.
/// </summary>
public sealed class PassengerSessionAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Banco dedicado às credenciais de passageiros.
    /// </summary>
    private readonly PassengerAuthDbContext _db;

    /// <summary>
    /// Serviço de hash dos tokens de sessão.
    /// </summary>
    private readonly SessionTokenService _tokens;

    /// <summary>
    /// Nomes de cookies corretos para o ambiente atual.
    /// </summary>
    private readonly CookieNameService _cookies;

    /// <summary>
    /// Construtor exigido pelo sistema de autenticação do ASP.NET Core.
    /// </summary>
    public PassengerSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        PassengerAuthDbContext db,
        SessionTokenService tokens,
        CookieNameService cookies)
        : base(options, logger, encoder)
    {
        _db = db;
        _tokens = tokens;
        _cookies = cookies;
    }

    /// <summary>
    /// Valida o cookie, a sessão, a expiração, a revogação e o SecurityStamp antes de criar o principal autenticado.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Ausência de cookie representa visitante e não um erro de autenticação.
        if (!Request.Cookies.TryGetValue(_cookies.PassengerSession, out var rawToken) ||
            string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticateResult.NoResult();
        }

        // Converte o token bruto em hash para consultar o banco sem armazenar o segredo.
        var hash = _tokens.HashToken(rawToken);

        // Carrega sessão e conta numa única consulta.
        var session = await _db.Sessions
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.TokenHash == hash);

        // Rejeita token inexistente, revogado ou expirado.
        if (session is null || session.RevogadoEm is not null || session.ExpiraEm <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Sessão inválida ou expirada.");
        }

        // Mudança de SecurityStamp invalida sessões antigas após troca de senha ou ação de segurança.
        if (session.SecurityStamp != (session.User.SecurityStamp ?? string.Empty))
        {
            return AuthenticateResult.Fail("Sessão invalidada por alteração de segurança.");
        }

        // Monta somente as claims necessárias à autorização da aplicação.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
            new(ClaimTypes.Name, session.User.Nome),
            new(ClaimTypes.Email, session.User.Email ?? string.Empty),
            new(ClaimTypes.Role, "Passageiro")
        };

        // Cria identidade vinculada ao esquema de passageiro.
        var identity = new ClaimsIdentity(claims, Scheme.Name);

        // Embala a identidade em principal e ticket padrão do ASP.NET Core.
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        // Informa sucesso ao pipeline de autorização.
        return AuthenticateResult.Success(ticket);
    }
}

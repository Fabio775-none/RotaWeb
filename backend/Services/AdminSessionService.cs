using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Security;

namespace RotaWeb.Api.Services;

/// <summary>
/// Sessão administrativa opaca, curta e revogável, emitida após senha válida.
/// Mantém compatibilidade com o esquema AdminSessions já existente no PostgreSQL.
/// </summary>
public sealed class AdminSessionService
{
    private readonly AdminAuthDbContext _db;
    private readonly SessionTokenService _tokens;
    private readonly CookieNameService _cookies;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AdminSessionService(AdminAuthDbContext db, SessionTokenService tokens,
        CookieNameService cookies, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _db = db;
        _tokens = tokens;
        _cookies = cookies;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task IssueAuthenticatedAsync(HttpContext httpContext, AdminUser user)
    {
        // Impede fixação da sessão: revoga sessão administrativa anterior e
        // cookies temporários de versões antigas antes de gerar token novo.
        await RevokeCookieAsync(httpContext, _cookies.AdminSession);
        await RevokeCookieAsync(httpContext, _cookies.AdminPreAuth);
        httpContext.Response.Cookies.Delete(_cookies.AdminPreAuth, BuildDeleteCookieOptions());

        var minutes = Math.Clamp(_configuration.GetValue("Security:AdminSessionMinutes", 20), 5, 240);
        var expires = DateTime.UtcNow.AddMinutes(minutes);
        var token = _tokens.CreateToken();
        _db.Sessions.Add(new AdminSession
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(token),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            Estagio = "Autenticada", // valor antigo preservado, sem migração de banco
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = expires,
            UserAgentHash = _tokens.HashUserAgent(httpContext.Request.Headers["User-Agent"].ToString())
        });
        await _db.SaveChangesAsync();
        httpContext.Response.Cookies.Append(_cookies.AdminSession, token, BuildCookieOptions(expires));
    }

    public async Task RevokeCurrentAdminAsync(HttpContext httpContext)
    {
        await RevokeCookieAsync(httpContext, _cookies.AdminSession);
        httpContext.Response.Cookies.Delete(_cookies.AdminSession, BuildDeleteCookieOptions());
    }

    private async Task RevokeCookieAsync(HttpContext httpContext, string cookieName)
    {
        if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var token) ||
            string.IsNullOrWhiteSpace(token)) return;

        var hash = _tokens.HashToken(token);
        var session = await _db.Sessions.FirstOrDefaultAsync(item => item.TokenHash == hash);
        if (session is not null && session.RevogadoEm is null)
        {
            session.RevogadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private CookieOptions BuildCookieOptions(DateTime expiresUtc) => new()
    {
        HttpOnly = true,
        Secure = !_environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = new DateTimeOffset(expiresUtc),
        MaxAge = expiresUtc - DateTime.UtcNow
    };

    private CookieOptions BuildDeleteCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = !_environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/"
    };
}

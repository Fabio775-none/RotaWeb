using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Endpoints auxiliares de segurança que não expõem credenciais.
/// </summary>
[ApiController]
[Route("api/security")]
public sealed class SecurityController : ControllerBase
{
    /// <summary>
    /// Serviço antiforgery nativo do ASP.NET Core.
    /// </summary>
    private readonly IAntiforgery _antiforgery;

    /// <summary>
    /// Inicializa o controller.
    /// </summary>
    public SecurityController(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    /// <summary>
    /// Entrega o token de requisição CSRF e grava o cookie antiforgery HttpOnly correspondente.
    /// </summary>
    [HttpGet("csrf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Csrf()
    {
        // Gera par cookie/token vinculado pelo Data Protection.
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        // O token de requisição pode ser lido pelo JavaScript e enviado no cabeçalho configurado.
        return Ok(new { token = tokens.RequestToken });
    }


    /// <summary>
    /// Entrega token CSRF vinculado à sessão administrativa já autenticada.
    /// A política AdminOnly força o esquema administrativo antes da emissão do token.
    /// </summary>
    [HttpGet("admin-csrf")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AdminCsrf()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    /// <summary>
    /// Healthcheck simples para confirmar que a API está respondendo sem consultar credenciais.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        // Retorna somente estado e horário UTC, sem informações internas do servidor.
        return Ok(new { status = "ok", utc = DateTime.UtcNow });
    }
}

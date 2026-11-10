namespace RotaWeb.Api.Security;

/// <summary>
/// Adiciona cabeçalhos defensivos a todas as respostas HTTP.
/// O objetivo é reduzir superfícies de XSS, clickjacking e interpretação indevida de conteúdo.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    /// <summary>
    /// Próximo middleware do pipeline.
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// Inicializa o middleware.
    /// </summary>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Aplica os cabeçalhos antes que a resposta seja enviada.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Impede sniffing de tipo MIME.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Bloqueia incorporação em frames de outros sites, mitigando clickjacking.
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Limita o envio de Referer para não vazar caminhos completos entre origens.
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Desativa APIs de navegador que o sistema não utiliza.
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        // CSP adequada ao frontend estático sem scripts inline.
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'";

        // Impede que navegadores e proxies armazenem respostas privadas de identidade e administração.
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/auth") ||
            path.StartsWithSegments("/api/admin") ||
            path.StartsWithSegments("/api/tickets") ||
            path.StartsWithSegments("/api/security"))
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        // Executa o restante do pipeline após preparar os cabeçalhos.
        await _next(context);
    }
}

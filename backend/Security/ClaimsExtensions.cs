using System.Security.Claims;

namespace RotaWeb.Api.Security;

/// <summary>
/// Funções pequenas para extrair identificadores autenticados sem repetir parsing em todos os controllers.
/// </summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Retorna o UUID do usuário autenticado ou lança exceção se o principal estiver inconsistente.
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        // NameIdentifier é criado somente pelo handler após validar a sessão.
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        // Falha fechada: principal sem UUID válido não pode prosseguir como usuário autenticado.
        if (!Guid.TryParse(raw, out var id))
        {
            throw new InvalidOperationException("Identidade autenticada não possui UUID válido.");
        }

        // Retorna o identificador validado.
        return id;
    }
}

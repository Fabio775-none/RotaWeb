namespace RotaWeb.Api.Models;

/// <summary>
/// Sessão administrativa opaca.
/// O campo Estagio mantém compatibilidade com registros das versões anteriores.
/// </summary>
public sealed class AdminSession
{
    /// <summary>
    /// Identificador interno da sessão.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Conta administrativa proprietária.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash do token opaco entregue ao navegador.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Cópia do SecurityStamp para revogação centralizada.
    /// </summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>
    /// Estágio da sessão: Autenticada para logins atuais (outros valores legados são rejeitados).
    /// </summary>
    public string Estagio { get; set; } = "Autenticada";

    /// <summary>
    /// Momento UTC de criação.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Momento UTC de expiração absoluta.
    /// </summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>
    /// Momento de revogação; nulo enquanto válida.
    /// </summary>
    public DateTime? RevogadoEm { get; set; }

    /// <summary>
    /// Hash do User-Agent para auditoria sem reter o texto completo.
    /// </summary>
    public string? UserAgentHash { get; set; }

    /// <summary>
    /// Navegação EF para a conta administrativa.
    /// </summary>
    public AdminUser User { get; set; } = null!;
}

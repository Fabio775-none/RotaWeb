namespace RotaWeb.Api.Models;

/// <summary>
/// Evento de auditoria administrativa.
/// Não armazena senhas, tokens ou conteúdo sensível; registra somente a ação e o alvo.
/// </summary>
public sealed class AuditEvent
{
    /// <summary>
    /// Identificador sequencial do evento.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// UUID da conta administrativa que executou a ação.
    /// </summary>
    public Guid AdminId { get; set; }

    /// <summary>
    /// Tipo de ação, por exemplo OnibusCriado ou ViagemCancelada.
    /// </summary>
    public string Acao { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do objeto afetado.
    /// </summary>
    public string Entidade { get; set; } = string.Empty;

    /// <summary>
    /// Identificador textual do objeto afetado.
    /// </summary>
    public string EntidadeId { get; set; } = string.Empty;

    /// <summary>
    /// Resumo técnico sem dados secretos.
    /// </summary>
    public string? Detalhes { get; set; }

    /// <summary>
    /// Instante UTC da ação.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

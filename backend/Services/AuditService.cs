using System.Security.Claims;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;

namespace RotaWeb.Api.Services;

/// <summary>
/// Serviço central de auditoria administrativa.
/// Mantém as controladoras simples e reduz o risco de esquecer registro em operações sensíveis.
/// </summary>
public sealed class AuditService
{
    /// <summary>
    /// Banco operacional onde fica a trilha de auditoria.
    /// </summary>
    private readonly BusinessDbContext _db;

    /// <summary>
    /// Inicializa o serviço com o banco operacional.
    /// </summary>
    public AuditService(BusinessDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Registra uma ação administrativa sem armazenar credenciais, tokens ou dados desnecessários.
    /// </summary>
    public async Task WriteAsync(ClaimsPrincipal principal, string action, string entity, string entityId, string? details = null)
    {
        // Obtém o identificador administrativo já validado pelo handler de sessão.
        var idText = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        // Uma ação sem identidade autenticada é erro de programação e não deve gerar auditoria falsa.
        if (!Guid.TryParse(idText, out var adminId))
        {
            throw new InvalidOperationException("Administrador autenticado não possui identificador válido.");
        }

        // Limita detalhes para evitar logs excessivos e dados sensíveis acidentais.
        var safeDetails = string.IsNullOrWhiteSpace(details)
            ? null
            : details.Length > 1000 ? details[..1000] : details;

        // Adiciona o evento com horário UTC.
        _db.AuditEvents.Add(new AuditEvent
        {
            AdminId = adminId,
            Acao = action,
            Entidade = entity,
            EntidadeId = entityId,
            Detalhes = safeDetails,
            CriadoEm = DateTime.UtcNow
        });

        // Persiste o evento.
        await _db.SaveChangesAsync();
    }
}

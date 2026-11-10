using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Indicadores compactos exibidos no topo da administração.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminDashboardController : ControllerBase
{
    /// <summary>
    /// Banco operacional.
    /// </summary>
    private readonly BusinessDbContext _db;

    /// <summary>
    /// Inicializa o controller.
    /// </summary>
    public AdminDashboardController(BusinessDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resume frota, viagens futuras e passagens confirmadas.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Summary()
    {
        // Conta ônibus ativos.
        var activeBuses = await _db.Onibus.CountAsync(bus => bus.Ativo);

        // Conta viagens futuras e programadas.
        var futureTrips = await _db.Viagens.CountAsync(trip => trip.Status == "Programada" && trip.Partida > DateTime.UtcNow);

        // Conta passagens atualmente confirmadas.
        var confirmedTickets = await _db.Passagens.CountAsync(ticket => ticket.Status == "Confirmada");

        // Soma receita histórica somente de confirmações que permanecem confirmadas.
        var confirmedValue = await _db.Passagens
            .Where(ticket => ticket.Status == "Confirmada")
            .SumAsync(ticket => (long?)ticket.ValorCentavos) ?? 0L;

        // Retorna números em centavos para manter precisão.
        return Ok(new { activeBuses, futureTrips, confirmedTickets, confirmedValueCents = confirmedValue });
    }
}

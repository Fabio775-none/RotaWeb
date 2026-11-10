using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Consulta pública de viagens e ocupação de assentos.
/// Dados pessoais de compradores nunca são expostos por esses endpoints.
/// </summary>
[ApiController]
[Route("api/trips")]
public sealed class TripsController : ControllerBase
{
    /// <summary>
    /// Banco operacional.
    /// </summary>
    private readonly BusinessDbContext _db;

    /// <summary>
    /// Inicializa o controller.
    /// </summary>
    public TripsController(BusinessDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Pesquisa viagens por origem, destino e data local de Brasília.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int? origemId,
        [FromQuery] int? destinoId,
        [FromQuery] DateOnly? data,
        [FromQuery] string? categoria,
        [FromQuery] string? ordenar)
    {
        // Parte somente de viagens programadas e ainda futuras.
        var query = _db.Viagens
            .AsNoTracking()
            .Include(trip => trip.Onibus)
            .Include(trip => trip.Origem)
            .Include(trip => trip.Destino)
            .Where(trip => trip.Status == "Programada" && trip.Partida > DateTime.UtcNow)
            .AsQueryable();

        // Aplica origem quando informada.
        if (origemId.HasValue)
        {
            query = query.Where(trip => trip.OrigemId == origemId.Value);
        }

        // Aplica destino quando informado.
        if (destinoId.HasValue)
        {
            query = query.Where(trip => trip.DestinoId == destinoId.Value);
        }

        // Converte o dia de Brasília para intervalo UTC para preservar comportamento correto no banco.
        if (data.HasValue)
        {
            var zone = GetBrasiliaTimeZone();
            var localStart = data.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var localEnd = data.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
            var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
            query = query.Where(trip => trip.Partida >= utcStart && trip.Partida < utcEnd);
        }

        // Categoria é comparada somente quando fornecida.
        if (!string.IsNullOrWhiteSpace(categoria) && !categoria.Equals("Todas", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCategory = categoria.Trim();
            query = query.Where(trip => trip.Onibus.Categoria == normalizedCategory);
        }

        // Ordenação aceita somente opções conhecidas; qualquer outro valor usa horário.
        query = ordenar?.Equals("preco", StringComparison.OrdinalIgnoreCase) == true
            ? query.OrderBy(trip => trip.PrecoCentavos).ThenBy(trip => trip.Partida)
            : query.OrderBy(trip => trip.Partida);

        // Projeta resultados e calcula disponibilidade sem revelar ocupantes.
        var results = await query
            .Select(trip => new
            {
                trip.Id,
                trip.Partida,
                trip.Chegada,
                trip.PrecoCentavos,
                trip.Status,
                origem = new { trip.Origem.Id, trip.Origem.Nome, trip.Origem.Uf },
                destino = new { trip.Destino.Id, trip.Destino.Nome, trip.Destino.Uf },
                onibus = new { trip.Onibus.Id, trip.Onibus.Prefixo, trip.Onibus.Modelo, trip.Onibus.Categoria, trip.Assentos },
                assentosDisponiveis = trip.Assentos - _db.Passagens.Count(ticket => ticket.ViagemId == trip.Id && ticket.Status == "Confirmada")
            })
            .Take(100)
            .ToListAsync();

        // Retorna no máximo cem opções por consulta para manter resposta previsível.
        return Ok(results);
    }

    /// <summary>
    /// Retorna detalhes públicos de uma viagem específica.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        // Projeta sem rastreamento porque é consulta somente leitura.
        var trip = await _db.Viagens
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Partida,
                item.Chegada,
                item.PrecoCentavos,
                item.Assentos,
                item.Status,
                origem = new { item.Origem.Id, item.Origem.Nome, item.Origem.Uf },
                destino = new { item.Destino.Id, item.Destino.Nome, item.Destino.Uf },
                onibus = new { item.Onibus.Id, item.Onibus.Prefixo, item.Onibus.Modelo, item.Onibus.Categoria },
                assentosDisponiveis = item.Assentos - _db.Passagens.Count(ticket => ticket.ViagemId == item.Id && ticket.Status == "Confirmada")
            })
            .FirstOrDefaultAsync();

        // 404 quando a viagem não existe.
        return trip is null ? NotFound(new { mensagem = "Viagem não encontrada." }) : Ok(trip);
    }

    /// <summary>
    /// Retorna apenas números de assentos ocupados para montar o mapa visual.
    /// </summary>
    [HttpGet("{id:int}/seats")]
    public async Task<IActionResult> Seats(int id)
    {
        // Confirma que a viagem existe e obtém capacidade.
        var capacity = await _db.Viagens
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => (int?)item.Assentos)
            .FirstOrDefaultAsync();

        // Viagem inexistente produz 404.
        if (!capacity.HasValue)
        {
            return NotFound(new { mensagem = "Viagem não encontrada." });
        }

        // Ocupação expõe somente números de poltrona, nunca passageiro ou documento.
        var occupied = await _db.Passagens
            .AsNoTracking()
            .Where(ticket => ticket.ViagemId == id && ticket.Status == "Confirmada")
            .Select(ticket => ticket.Assento)
            .OrderBy(seat => seat)
            .ToListAsync();

        // Retorna capacidade e ocupação.
        return Ok(new { assentos = capacity.Value, ocupados = occupied });
    }

    /// <summary>
    /// Obtém fuso de Brasília em Windows e Linux.
    /// </summary>
    private static TimeZoneInfo GetBrasiliaTimeZone()
    {
        // Windows utiliza o identificador histórico da Microsoft.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            // Linux e containers normalmente usam o identificador IANA.
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
    }
}

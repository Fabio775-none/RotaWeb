using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Contracts;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Services;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// CRUD administrativo de viagens com validações de horário, veículo, rota e ocupação.
/// </summary>
[ApiController]
[Route("api/admin/trips")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminTripsController : ControllerBase
{
    /// <summary>
    /// Banco operacional.
    /// </summary>
    private readonly BusinessDbContext _db;

    /// <summary>
    /// Serviço oficial de localidades para validar cidades recebidas.
    /// </summary>
    private readonly IbgeService _ibge;

    /// <summary>
    /// Auditoria administrativa.
    /// </summary>
    private readonly AuditService _audit;

    /// <summary>
    /// Inicializa as dependências.
    /// </summary>
    public AdminTripsController(BusinessDbContext db, IbgeService ibge, AuditService audit)
    {
        _db = db;
        _ibge = ibge;
        _audit = audit;
    }

    /// <summary>
    /// Lista viagens com rota, veículo, ocupação e status calculado.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        // Projeta indicadores necessários à tela administrativa.
        var trips = await _db.Viagens
            .AsNoTracking()
            .OrderByDescending(trip => trip.Partida)
            .Select(trip => new
            {
                trip.Id,
                trip.Partida,
                trip.Chegada,
                trip.PrecoCentavos,
                trip.Assentos,
                status = trip.Status == "Programada" && trip.Chegada <= DateTime.UtcNow ? "Encerrada" : trip.Status,
                origem = new { trip.Origem.Id, trip.Origem.Nome, trip.Origem.Uf },
                destino = new { trip.Destino.Id, trip.Destino.Nome, trip.Destino.Uf },
                onibus = new { trip.Onibus.Id, trip.Onibus.Prefixo, trip.Onibus.Modelo, trip.Onibus.Categoria },
                ocupados = _db.Passagens.Count(ticket => ticket.ViagemId == trip.Id && ticket.Status == "Confirmada")
            })
            .Take(500)
            .ToListAsync();

        // Retorna até quinhentas viagens recentes.
        return Ok(trips);
    }

    /// <summary>
    /// Cria uma viagem futura depois de validar cidades no IBGE e conflito de agenda do ônibus.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TripRequest request, CancellationToken cancellationToken)
    {
        // Centraliza validações e persistência das cidades antes de criar a viagem.
        var validation = await ValidateTripRequestAsync(request, null, cancellationToken);
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        // O ônibus validado está ativo e existe.
        var bus = validation.Bus!;

        // Cria a viagem com capacidade copiada do ônibus.
        var trip = new Viagem
        {
            OnibusId = bus.Id,
            OrigemId = request.OrigemId,
            DestinoId = request.DestinoId,
            Partida = request.Partida.UtcDateTime,
            Chegada = request.Chegada.UtcDateTime,
            PrecoCentavos = request.PrecoCentavos,
            Assentos = bus.Assentos,
            Status = "Programada"
        };

        // Persiste viagem.
        _db.Viagens.Add(trip);
        await _db.SaveChangesAsync(cancellationToken);

        // Audita criação.
        await _audit.WriteAsync(User, "ViagemCriada", "Viagem", trip.Id.ToString(), $"Onibus={bus.Prefixo}");

        // Retorna ID criado.
        return StatusCode(StatusCodes.Status201Created, new { trip.Id, mensagem = "Viagem cadastrada." });
    }

    /// <summary>
    /// Edita viagem futura somente quando ainda não existem passagens confirmadas.
    /// </summary>
    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, TripRequest request, CancellationToken cancellationToken)
    {
        // Carrega a viagem para edição.
        var trip = await _db.Viagens.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null)
        {
            return NotFound(new { mensagem = "Viagem não encontrada." });
        }

        // Só viagens futuras e programadas podem ser editadas.
        if (trip.Status != "Programada" || trip.Partida <= DateTime.UtcNow)
        {
            return Conflict(new { mensagem = "Somente viagens futuras e programadas podem ser editadas." });
        }

        // Passagens confirmadas congelam dados comerciais e operacionais da viagem.
        if (await _db.Passagens.AnyAsync(ticket => ticket.ViagemId == id && ticket.Status == "Confirmada", cancellationToken))
        {
            return Conflict(new { mensagem = "Viagens com passagens confirmadas não podem ser editadas." });
        }

        // Reutiliza todas as validações ignorando a própria viagem ao verificar sobreposição.
        var validation = await ValidateTripRequestAsync(request, id, cancellationToken);
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        // Atualiza a viagem e recopia capacidade do ônibus escolhido.
        trip.OnibusId = validation.Bus!.Id;
        trip.OrigemId = request.OrigemId;
        trip.DestinoId = request.DestinoId;
        trip.Partida = request.Partida.UtcDateTime;
        trip.Chegada = request.Chegada.UtcDateTime;
        trip.PrecoCentavos = request.PrecoCentavos;
        trip.Assentos = validation.Bus.Assentos;
        await _db.SaveChangesAsync(cancellationToken);

        // Audita edição.
        await _audit.WriteAsync(User, "ViagemEditada", "Viagem", trip.Id.ToString());

        // Confirma atualização.
        return Ok(new { mensagem = "Viagem atualizada." });
    }

    /// <summary>
    /// Cancela uma viagem futura e suas passagens confirmadas em uma única transação.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        // Transação serializável evita confirmação simultânea de passagem durante o cancelamento.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Carrega viagem sob bloqueio de linha.
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Viagens\" WHERE \"Id\" = {id} FOR UPDATE");
        var trip = await _db.Viagens.FirstOrDefaultAsync(item => item.Id == id);

        // Viagem precisa existir.
        if (trip is null)
        {
            return NotFound(new { mensagem = "Viagem não encontrada." });
        }

        // Só pode cancelar antes da partida.
        if (trip.Partida <= DateTime.UtcNow)
        {
            return Conflict(new { mensagem = "Não é possível cancelar uma viagem após a partida." });
        }

        // Não repete cancelamento.
        if (trip.Status == "Cancelada")
        {
            return Conflict(new { mensagem = "Viagem já cancelada." });
        }

        // Marca a viagem como cancelada.
        trip.Status = "Cancelada";

        // Carrega passagens confirmadas dessa viagem.
        var tickets = await _db.Passagens
            .Where(ticket => ticket.ViagemId == id && ticket.Status == "Confirmada")
            .ToListAsync();

        // Mantém histórico e marca cada passagem como cancelada.
        var now = DateTime.UtcNow;
        foreach (var ticket in tickets)
        {
            ticket.Status = "Cancelada";
            ticket.CanceladoEm = now;
        }

        // Persiste viagem e passagens juntas.
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        // Audita a ação destrutiva.
        await _audit.WriteAsync(User, "ViagemCancelada", "Viagem", trip.Id.ToString(), $"PassagensCanceladas={tickets.Count}");

        // Confirma resultado.
        return Ok(new { mensagem = "Viagem cancelada.", passagensCanceladas = tickets.Count });
    }

    /// <summary>
    /// Exclui fisicamente uma viagem somente quando não existe nenhuma passagem vinculada.
    /// Para viagens com histórico, o fluxo correto continua sendo Cancelar.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var trip = await _db.Viagens.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null)
        {
            return NotFound(new { mensagem = "Viagem não encontrada." });
        }

        var hasTickets = await _db.Passagens.AnyAsync(ticket => ticket.ViagemId == id, cancellationToken);
        if (hasTickets)
        {
            return Conflict(new { mensagem = "A viagem possui passagens vinculadas e não pode ser excluída. Use Cancelar para preservar o histórico." });
        }

        _db.Viagens.Remove(trip);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(User, "ViagemExcluida", "Viagem", id.ToString());

        return Ok(new { mensagem = "Viagem excluída." });
    }

    /// <summary>
    /// Valida uma requisição de viagem e garante cidades persistidas no banco operacional.
    /// </summary>
    private async Task<(Onibus? Bus, IActionResult? Error)> ValidateTripRequestAsync(
        TripRequest request,
        int? currentTripId,
        CancellationToken cancellationToken)
    {
        // Origem e destino precisam ser diferentes.
        if (request.OrigemId == request.DestinoId)
        {
            return (null, BadRequest(new { mensagem = "Origem e destino devem ser diferentes." }));
        }

        // Partida precisa ser futura.
        if (request.Partida.UtcDateTime <= DateTime.UtcNow)
        {
            return (null, BadRequest(new { mensagem = "A partida deve ser futura." }));
        }

        // Chegada precisa ser posterior e duração máxima é de sete dias.
        var duration = request.Chegada.UtcDateTime - request.Partida.UtcDateTime;
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(7))
        {
            return (null, BadRequest(new { mensagem = "Chegada deve ser posterior à partida e a duração máxima é de sete dias." }));
        }

        // Ônibus precisa existir e estar ativo.
        var bus = await _db.Onibus.FirstOrDefaultAsync(item => item.Id == request.OnibusId && item.Ativo, cancellationToken);
        if (bus is null)
        {
            return (null, BadRequest(new { mensagem = "Ônibus inexistente ou inativo." }));
        }

        // Confere conflito de horário: intervalos se sobrepõem quando início A < fim B e fim A > início B.
        var start = request.Partida.UtcDateTime;
        var end = request.Chegada.UtcDateTime;
        var overlap = await _db.Viagens.AnyAsync(trip =>
            trip.OnibusId == request.OnibusId &&
            trip.Status == "Programada" &&
            (!currentTripId.HasValue || trip.Id != currentTripId.Value) &&
            start < trip.Chegada && end > trip.Partida,
            cancellationToken);

        if (overlap)
        {
            return (null, Conflict(new { mensagem = "O ônibus já possui viagem sobreposta nesse horário." }));
        }

        // Consulta ambas as cidades na origem oficial para não confiar em nomes enviados pelo cliente.
        var origin = await _ibge.GetCityAsync(request.OrigemId, cancellationToken);
        var destination = await _ibge.GetCityAsync(request.DestinoId, cancellationToken);
        if (origin is null || destination is null || string.IsNullOrWhiteSpace(origin.GetUf()) || string.IsNullOrWhiteSpace(destination.GetUf()))
        {
            return (null, BadRequest(new { mensagem = "Não foi possível validar origem ou destino no IBGE." }));
        }

        // Persiste cidade de origem somente se ainda não existir.
        if (!await _db.Cidades.AnyAsync(city => city.Id == origin.Id, cancellationToken))
        {
            _db.Cidades.Add(new Cidade { Id = origin.Id, Nome = origin.Nome, Uf = origin.GetUf().ToUpperInvariant() });
        }

        // Persiste cidade de destino somente se ainda não existir.
        if (!await _db.Cidades.AnyAsync(city => city.Id == destination.Id, cancellationToken))
        {
            _db.Cidades.Add(new Cidade { Id = destination.Id, Nome = destination.Nome, Uf = destination.GetUf().ToUpperInvariant() });
        }

        // Salva cidades antes da criação/edição da viagem para satisfazer FKs.
        await _db.SaveChangesAsync(cancellationToken);

        // Retorna o ônibus validado.
        return (bus, null);
    }
}

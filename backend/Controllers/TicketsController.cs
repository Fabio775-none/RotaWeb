using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Contracts;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Security;
using RotaWeb.Api.Services;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Compra, consulta e cancelamento de passagens do passageiro autenticado.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Authorize(Policy = "PassengerOnly")]
public sealed class TicketsController : ControllerBase
{
    /// <summary>
    /// Banco operacional com viagens e passagens.
    /// </summary>
    private readonly BusinessDbContext _db;
    private readonly UserManager<PassengerUser> _users;

    /// <summary>
    /// Inicializa o controller.
    /// </summary>
    public TicketsController(BusinessDbContext db, UserManager<PassengerUser> users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>
    /// Confirma uma passagem garantindo preço, capacidade e exclusividade da poltrona.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTicketRequest request)
    {
        // Identifica o comprador pela sessão autenticada e nunca por um campo enviado pelo cliente.
        var userId = User.GetRequiredUserId();
        var buyerError = await ValidateAdultBuyerAsync(userId);
        if (buyerError is not null) return buyerError;

        // A transação serializável reduz corrida entre compras simultâneas da mesma viagem.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Bloqueia a linha da viagem no PostgreSQL durante a validação/compra concorrente.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Viagens\" WHERE \"Id\" = {request.ViagemId} FOR UPDATE");

        // Recarrega a viagem após adquirir o lock.
        var trip = await _db.Viagens
            .Include(item => item.Onibus)
            .FirstOrDefaultAsync(item => item.Id == request.ViagemId);

        // A compra exige viagem existente.
        if (trip is null)
        {
            return NotFound(new { mensagem = "Viagem não encontrada." });
        }

        // Somente viagem programada e ainda futura pode receber nova passagem.
        if (trip.Status != "Programada" || trip.Partida <= DateTime.UtcNow)
        {
            return Conflict(new { mensagem = "Esta viagem não aceita novas passagens." });
        }

        var passengerError = ValidatePassengerForTrip(
            request.DataNascimento, request.TipoDocumento, request.ResponsavelNome, request.ResponsavelDocumento,
            request.ResponsavelDataNascimento, request.ResponsavelVinculo, request.ResponsavelAcompanha,
            request.DocumentacaoMenorConfirmada, trip.Partida);
        if (passengerError is not null) return passengerError;

        // O assento precisa caber na capacidade congelada da viagem.
        if (request.Assento < 1 || request.Assento > trip.Assentos)
        {
            return BadRequest(new { mensagem = "Assento fora da capacidade da viagem." });
        }

        // Confere preço exibido para impedir confirmação silenciosa após alteração administrativa.
        if (request.PrecoEsperadoCentavos != trip.PrecoCentavos)
        {
            return Conflict(new
            {
                mensagem = "O preço da viagem foi alterado. Atualize a busca antes de confirmar.",
                precoAtualCentavos = trip.PrecoCentavos
            });
        }

        // Confere ocupação sob o lock antes de tentar inserir.
        var occupied = await _db.Passagens.AnyAsync(ticket =>
            ticket.ViagemId == request.ViagemId &&
            ticket.Assento == request.Assento &&
            ticket.Status == "Confirmada");

        // Se outro comprador confirmou primeiro, a poltrona deve ser escolhida novamente.
        if (occupied)
        {
            return Conflict(new { mensagem = "Este assento não está mais disponível." });
        }

        // Monta registro histórico com preço copiado da viagem.
        var ticket = new Passagem
        {
            Id = Guid.NewGuid(),
            Codigo = CreateTicketCode(),
            ViagemId = trip.Id,
            UsuarioId = userId,
            Assento = request.Assento,
            Passageiro = request.Passageiro.Trim(),
            Documento = request.Documento.Trim(),
            TipoDocumento = request.TipoDocumento.Trim(),
            DataNascimento = request.DataNascimento,
            ResponsavelNome = Clean(request.ResponsavelNome),
            ResponsavelDocumento = Clean(request.ResponsavelDocumento),
            ResponsavelDataNascimento = request.ResponsavelDataNascimento,
            ResponsavelVinculo = Clean(request.ResponsavelVinculo),
            ResponsavelAcompanha = request.ResponsavelAcompanha,
            DocumentacaoMenorConfirmada = request.DocumentacaoMenorConfirmada,
            ValorCentavos = trip.PrecoCentavos,
            Status = "Confirmada",
            CompradoEm = DateTime.UtcNow
        };

        // Adiciona ao contexto.
        _db.Passagens.Add(ticket);

        try
        {
            // Persiste e deixa o índice único parcial funcionar como última barreira contra corrida.
            await _db.SaveChangesAsync();

            // Confirma a transação somente depois de todas as garantias.
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Uma colisão tardia de índice é apresentada como assento indisponível, sem detalhes internos do banco.
            await transaction.RollbackAsync();
            return Conflict(new { mensagem = "O assento foi ocupado por outra compra. Escolha outra poltrona." });
        }

        // Retorna identificadores seguros para abrir o comprovante.
        return StatusCode(StatusCodes.Status201Created, new
        {
            ticket.Id,
            ticket.Codigo,
            mensagem = "Passagem confirmada com sucesso."
        });
    }


    /// <summary>
    /// Confirma de uma a cinco poltronas em uma única transação.
    /// O código Pix associado é apenas uma referência demonstrativa do projeto escolar.
    /// </summary>
    [HttpPost("batch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatch(CreateTicketBatchRequest request)
    {
        // O limite de cinco lugares é regra de negócio e também é conferido no servidor.
        if (request.Passageiros is null || request.Passageiros.Count < 1 || request.Passageiros.Count > 5)
        {
            return BadRequest(new { mensagem = "Escolha de 1 a 5 poltronas por compra." });
        }

        // Uma mesma poltrona não pode aparecer duas vezes no lote enviado pelo navegador.
        var requestedSeats = request.Passageiros.Select(item => item.Assento).ToList();
        if (requestedSeats.Distinct().Count() != requestedSeats.Count)
        {
            return BadRequest(new { mensagem = "Há poltronas repetidas na compra." });
        }

        // Reforça as validações de cada passageiro mesmo que o frontend seja alterado.
        foreach (var passenger in request.Passageiros)
        {
            if (passenger.Assento < 1 || passenger.Assento > 60 ||
                string.IsNullOrWhiteSpace(passenger.Passageiro) || passenger.Passageiro.Trim().Length < 3 ||
                string.IsNullOrWhiteSpace(passenger.Documento) || passenger.Documento.Trim().Length < 5)
            {
                return BadRequest(new { mensagem = "Revise nome, documento e poltrona de todos os passageiros." });
            }
        }

        // Identifica o comprador somente pela sessão autenticada.
        var userId = User.GetRequiredUserId();
        var buyerError = await ValidateAdultBuyerAsync(userId);
        if (buyerError is not null) return buyerError;

        // Uma única transação protege o lote: ou todas as poltronas são emitidas, ou nenhuma é.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Bloqueia a viagem durante a conferência de disponibilidade.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Viagens\" WHERE \"Id\" = {request.ViagemId} FOR UPDATE");

        var trip = await _db.Viagens
            .Include(item => item.Onibus)
            .FirstOrDefaultAsync(item => item.Id == request.ViagemId);

        if (trip is null)
        {
            return NotFound(new { mensagem = "Viagem não encontrada." });
        }

        if (trip.Status != "Programada" || trip.Partida <= DateTime.UtcNow)
        {
            return Conflict(new { mensagem = "Esta viagem não aceita novas passagens." });
        }

        if (request.PrecoEsperadoCentavos != trip.PrecoCentavos)
        {
            return Conflict(new
            {
                mensagem = "O preço da viagem foi alterado. Atualize a busca antes de confirmar.",
                precoAtualCentavos = trip.PrecoCentavos
            });
        }

        foreach (var passenger in request.Passageiros)
        {
            var passengerError = ValidatePassengerForTrip(
                passenger.DataNascimento, passenger.TipoDocumento, passenger.ResponsavelNome, passenger.ResponsavelDocumento,
                passenger.ResponsavelDataNascimento, passenger.ResponsavelVinculo, passenger.ResponsavelAcompanha,
                passenger.DocumentacaoMenorConfirmada, trip.Partida);
            if (passengerError is not null) return passengerError;
        }

        if (requestedSeats.Any(seat => seat < 1 || seat > trip.Assentos))
        {
            return BadRequest(new { mensagem = "Uma das poltronas está fora da capacidade do ônibus." });
        }

        // Consulta todas as colisões de uma vez para informar ao usuário quais lugares mudaram de estado.
        var occupiedSeats = await _db.Passagens
            .Where(ticket => ticket.ViagemId == request.ViagemId &&
                             ticket.Status == "Confirmada" &&
                             requestedSeats.Contains(ticket.Assento))
            .Select(ticket => ticket.Assento)
            .OrderBy(seat => seat)
            .ToListAsync();

        if (occupiedSeats.Count > 0)
        {
            return Conflict(new
            {
                mensagem = $"As poltronas {string.Join(", ", occupiedSeats)} não estão mais disponíveis.",
                ocupadas = occupiedSeats
            });
        }

        var tickets = request.Passageiros.Select(passenger => new Passagem
        {
            Id = Guid.NewGuid(),
            Codigo = CreateTicketCode(),
            ViagemId = trip.Id,
            UsuarioId = userId,
            Assento = passenger.Assento,
            Passageiro = passenger.Passageiro.Trim(),
            Documento = passenger.Documento.Trim(),
            TipoDocumento = passenger.TipoDocumento.Trim(),
            DataNascimento = passenger.DataNascimento,
            ResponsavelNome = Clean(passenger.ResponsavelNome),
            ResponsavelDocumento = Clean(passenger.ResponsavelDocumento),
            ResponsavelDataNascimento = passenger.ResponsavelDataNascimento,
            ResponsavelVinculo = Clean(passenger.ResponsavelVinculo),
            ResponsavelAcompanha = passenger.ResponsavelAcompanha,
            DocumentacaoMenorConfirmada = passenger.DocumentacaoMenorConfirmada,
            ValorCentavos = trip.PrecoCentavos,
            Status = "Confirmada",
            CompradoEm = DateTime.UtcNow
        }).ToList();

        _db.Passagens.AddRange(tickets);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { mensagem = "Uma das poltronas foi ocupada por outra compra. Atualize o mapa e tente novamente." });
        }

        // O pagamento é intencionalmente demonstrativo: o projeto não conversa com banco ou PSP real.
        return StatusCode(StatusCodes.Status201Created, new
        {
            mensagem = tickets.Count == 1 ? "Passagem emitida com sucesso." : $"{tickets.Count} passagens emitidas com sucesso.",
            pagamentoDemo = "Aprovado",
            referenciaPagamentoDemo = request.ReferenciaPagamentoDemo,
            totalCentavos = trip.PrecoCentavos * tickets.Count,
            tickets = tickets.Select(ticket => new
            {
                ticket.Id,
                ticket.Codigo,
                ticket.Assento,
                ticket.Passageiro
            })
        });
    }

    /// <summary>
    /// Lista somente passagens pertencentes à conta autenticada.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] string? status)
    {
        // Obtém o comprador pela sessão.
        var userId = User.GetRequiredUserId();

        // Filtra pelo proprietário antes de qualquer outro critério.
        var query = _db.Passagens
            .AsNoTracking()
            .Include(ticket => ticket.Viagem).ThenInclude(trip => trip.Origem)
            .Include(ticket => ticket.Viagem).ThenInclude(trip => trip.Destino)
            .Where(ticket => ticket.UsuarioId == userId)
            .AsQueryable();

        // Status opcional aceita apenas os dois estados definidos.
        if (!string.IsNullOrWhiteSpace(status) &&
            (status == "Confirmada" || status == "Cancelada"))
        {
            query = query.Where(ticket => ticket.Status == status);
        }

        // Retorna mais recentes primeiro.
        var tickets = await query
            .OrderByDescending(ticket => ticket.CompradoEm)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.Codigo,
                ticket.Assento,
                ticket.Passageiro,
                ticket.DataNascimento,
                ticket.TipoDocumento,
                ticket.ResponsavelNome,
                ticket.ResponsavelDocumento,
                ticket.ResponsavelDataNascimento,
                ticket.ResponsavelVinculo,
                ticket.ResponsavelAcompanha,
                ticket.DocumentacaoMenorConfirmada,
                ticket.ValorCentavos,
                ticket.Status,
                ticket.CompradoEm,
                ticket.CanceladoEm,
                viagem = new
                {
                    ticket.Viagem.Id,
                    ticket.Viagem.Partida,
                    ticket.Viagem.Chegada,
                    origem = new { ticket.Viagem.Origem.Nome, ticket.Viagem.Origem.Uf },
                    destino = new { ticket.Viagem.Destino.Nome, ticket.Viagem.Destino.Uf }
                }
            })
            .ToListAsync();

        // Lista pertence exclusivamente ao usuário atual.
        return Ok(tickets);
    }

    /// <summary>
    /// Abre uma passagem específica somente para seu comprador.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Restringe por ID e usuário na própria consulta para evitar IDOR.
        var userId = User.GetRequiredUserId();
        var ticket = await _db.Passagens
            .AsNoTracking()
            .Include(item => item.Viagem).ThenInclude(trip => trip.Origem)
            .Include(item => item.Viagem).ThenInclude(trip => trip.Destino)
            .Include(item => item.Viagem).ThenInclude(trip => trip.Onibus)
            .Where(item => item.Id == id && item.UsuarioId == userId)
            .Select(item => new
            {
                item.Id,
                item.Codigo,
                item.Assento,
                item.Passageiro,
                item.Documento,
                item.TipoDocumento,
                item.DataNascimento,
                item.ResponsavelNome,
                item.ResponsavelDocumento,
                item.ResponsavelDataNascimento,
                item.ResponsavelVinculo,
                item.ResponsavelAcompanha,
                item.DocumentacaoMenorConfirmada,
                item.ValorCentavos,
                item.Status,
                item.CompradoEm,
                item.CanceladoEm,
                viagem = new
                {
                    item.Viagem.Id,
                    item.Viagem.Partida,
                    item.Viagem.Chegada,
                    origem = new { item.Viagem.Origem.Nome, item.Viagem.Origem.Uf },
                    destino = new { item.Viagem.Destino.Nome, item.Viagem.Destino.Uf },
                    onibus = new { item.Viagem.Onibus.Prefixo, item.Viagem.Onibus.Modelo, item.Viagem.Onibus.Categoria }
                }
            })
            .FirstOrDefaultAsync();

        // Não diferencia inexistente de pertencente a outra pessoa.
        return ticket is null ? NotFound(new { mensagem = "Passagem não encontrada." }) : Ok(ticket);
    }

    /// <summary>
    /// Cancela uma passagem do próprio comprador antes da partida, mantendo o histórico.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        // Restringe consulta ao comprador autenticado.
        var userId = User.GetRequiredUserId();
        var ticket = await _db.Passagens
            .Include(item => item.Viagem)
            .FirstOrDefaultAsync(item => item.Id == id && item.UsuarioId == userId);

        // Resposta única para registro inexistente ou de outro usuário.
        if (ticket is null)
        {
            return NotFound(new { mensagem = "Passagem não encontrada." });
        }

        // Registro já cancelado não é alterado novamente.
        if (ticket.Status != "Confirmada")
        {
            return Conflict(new { mensagem = "Esta passagem já está cancelada." });
        }

        // Cancelamento só é permitido antes da partida.
        if (ticket.Viagem.Partida <= DateTime.UtcNow)
        {
            return Conflict(new { mensagem = "Não é possível cancelar após o horário de partida." });
        }

        // Mantém o histórico, muda o status e libera o índice parcial da poltrona.
        ticket.Status = "Cancelada";
        ticket.CanceladoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Confirma sem remover dados históricos.
        return Ok(new { mensagem = "Passagem cancelada com sucesso." });
    }

    private async Task<IActionResult?> ValidateAdultBuyerAsync(Guid userId)
    {
        var buyer = await _users.FindByIdAsync(userId.ToString());
        if (buyer?.DataNascimento is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                mensagem = "Atualize sua data de nascimento no Perfil antes de comprar. O comprador deve ter 18 anos ou mais."
            });
        }

        if (!AgePolicy.IsAdult(buyer.DataNascimento.Value, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                mensagem = "A compra deve ser realizada por uma pessoa com 18 anos ou mais."
            });
        }

        return null;
    }

    private static IActionResult? ValidatePassengerForTrip(
        DateOnly? birthDate, string? documentType, string? guardianName, string? guardianDocument,
        DateOnly? guardianBirthDate, string? guardianRelation, bool guardianAccompanies,
        bool minorDocumentationConfirmed, DateTime departureUtc)
    {
        if (!birthDate.HasValue)
        {
            return new BadRequestObjectResult(new { mensagem = "Informe a data de nascimento de todos os passageiros." });
        }

        var travelDate = DateOnly.FromDateTime(departureUtc);
        if (birthDate.Value > travelDate)
        {
            return new BadRequestObjectResult(new { mensagem = "A data de nascimento de um passageiro é inválida." });
        }

        var age = AgePolicy.CalculateAge(birthDate.Value, travelDate);
        var normalizedDocumentType = documentType?.Trim();
        var acceptedDocumentTypes = new[] { "RG/CIN", "Passaporte", "Certidão de nascimento" };
        if (string.IsNullOrWhiteSpace(normalizedDocumentType) || !acceptedDocumentTypes.Contains(normalizedDocumentType))
        {
            return new BadRequestObjectResult(new { mensagem = "Selecione um tipo de documento válido para cada passageiro." });
        }

        // Para 12 anos completos ou mais, a ANTT exige documento oficial com foto em viagens interestaduais.
        if (age >= 12 && normalizedDocumentType == "Certidão de nascimento")
        {
            return new BadRequestObjectResult(new { mensagem = "Passageiros com 12 anos ou mais devem usar documento oficial com foto (RG/CIN ou passaporte)." });
        }

        if (age >= 18)
        {
            return null;
        }

        // Política protetiva do RotaWeb: todo passageiro menor de 18 anos deve estar acompanhado
        // por responsável adulto identificado, mesmo quando a legislação permite 16/17 anos desacompanhados.
        if (string.IsNullOrWhiteSpace(guardianName) || guardianName.Trim().Length < 3 ||
            string.IsNullOrWhiteSpace(guardianDocument) || guardianDocument.Trim().Length < 3 ||
            !guardianBirthDate.HasValue || string.IsNullOrWhiteSpace(guardianRelation) || !guardianAccompanies)
        {
            return new BadRequestObjectResult(new
            {
                mensagem = "Passageiro menor de 18 anos exige responsável adulto identificado e acompanhante."
            });
        }

        if (!AgePolicy.IsAdult(guardianBirthDate.Value, travelDate))
        {
            return new BadRequestObjectResult(new { mensagem = "O responsável informado deve ter 18 anos ou mais na data da viagem." });
        }

        if (!minorDocumentationConfirmed)
        {
            return new BadRequestObjectResult(new
            {
                mensagem = "Confirme que a documentação exigida para o menor e para o responsável será apresentada no embarque."
            });
        }

        return null;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Cria código público não sequencial com prefixo RW.
    /// </summary>
    private static string CreateTicketCode()
    {
        // Usa 14 caracteres hexadecimais de um UUID aleatório, em maiúsculas.
        return $"RW{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    }
}

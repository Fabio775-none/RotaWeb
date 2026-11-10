using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Contracts;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;
using RotaWeb.Api.Services;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// CRUD administrativo da frota.
/// Exclusões físicas não são usadas; desativação preserva histórico e integridade.
/// </summary>
[ApiController]
[Route("api/admin/buses")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminBusesController : ControllerBase
{
    /// <summary>
    /// Banco operacional.
    /// </summary>
    private readonly BusinessDbContext _db;

    /// <summary>
    /// Serviço de trilha de auditoria.
    /// </summary>
    private readonly AuditService _audit;

    /// <summary>
    /// Inicializa dependências.
    /// </summary>
    public AdminBusesController(BusinessDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// Lista a frota completa para a administração.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        // Ordenação por prefixo facilita demonstração e operação.
        var buses = await _db.Onibus.AsNoTracking().OrderBy(bus => bus.Prefixo).ToListAsync();

        // Retorna entidades sem dados sensíveis, pois ônibus são dados operacionais.
        return Ok(buses);
    }

    /// <summary>
    /// Cadastra um novo ônibus com prefixo e placa únicos.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusRequest request)
    {
        // Normaliza campos identificadores antes de consultar unicidade.
        var prefix = request.Prefixo.Trim().ToUpperInvariant();
        var plate = request.Placa.Trim().ToUpperInvariant();
        var category = NormalizeCategory(request.Categoria);

        // Rejeita categorias fora do escopo documentado.
        if (category is null)
        {
            return BadRequest(new { mensagem = "Categoria de ônibus inválida." });
        }

        // Garante unicidade lógica antes da tentativa de insert.
        if (await _db.Onibus.AnyAsync(bus => bus.Prefixo == prefix || bus.Placa == plate))
        {
            return Conflict(new { mensagem = "Prefixo ou placa já cadastrados." });
        }

        // Cria o veículo ativo por padrão.
        var bus = new Onibus
        {
            Prefixo = prefix,
            Placa = plate,
            Modelo = request.Modelo.Trim(),
            Categoria = category,
            Assentos = request.Assentos,
            Ativo = true
        };

        // Persiste o novo ônibus.
        _db.Onibus.Add(bus);
        await _db.SaveChangesAsync();

        // Registra operação administrativa sem credenciais.
        await _audit.WriteAsync(User, "OnibusCriado", "Onibus", bus.Id.ToString(), $"Prefixo={bus.Prefixo}");

        // Retorna o objeto criado.
        return StatusCode(StatusCodes.Status201Created, bus);
    }

    /// <summary>
    /// Edita dados da frota respeitando a regra de capacidade congelada quando há viagens futuras ou em andamento.
    /// </summary>
    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, BusRequest request)
    {
        // Carrega o registro controlado pelo administrador.
        var bus = await _db.Onibus.FirstOrDefaultAsync(item => item.Id == id);
        if (bus is null)
        {
            return NotFound(new { mensagem = "Ônibus não encontrado." });
        }

        // Normaliza identificadores e categoria.
        var prefix = request.Prefixo.Trim().ToUpperInvariant();
        var plate = request.Placa.Trim().ToUpperInvariant();
        var category = NormalizeCategory(request.Categoria);
        if (category is null)
        {
            return BadRequest(new { mensagem = "Categoria de ônibus inválida." });
        }

        // Impede colisão com outro veículo.
        if (await _db.Onibus.AnyAsync(item => item.Id != id && (item.Prefixo == prefix || item.Placa == plate)))
        {
            return Conflict(new { mensagem = "Prefixo ou placa já utilizados por outro ônibus." });
        }

        // Mudança de capacidade é bloqueada quando há viagem futura ou em andamento associada.
        if (bus.Assentos != request.Assentos)
        {
            var blocksCapacityChange = await _db.Viagens.AnyAsync(trip =>
                trip.OnibusId == id &&
                trip.Status == "Programada" &&
                trip.Chegada > DateTime.UtcNow);

            if (blocksCapacityChange)
            {
                return Conflict(new { mensagem = "Não é possível alterar a capacidade com viagens futuras ou em andamento." });
            }
        }

        // Aplica as mudanças permitidas.
        bus.Prefixo = prefix;
        bus.Placa = plate;
        bus.Modelo = request.Modelo.Trim();
        bus.Categoria = category;
        bus.Assentos = request.Assentos;
        await _db.SaveChangesAsync();

        // Audita a edição.
        await _audit.WriteAsync(User, "OnibusEditado", "Onibus", bus.Id.ToString(), $"Prefixo={bus.Prefixo}");

        // Confirma atualização.
        return Ok(bus);
    }

    /// <summary>
    /// Desativa um ônibus sem apagar histórico.
    /// </summary>
    [HttpPost("{id:int}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        // Localiza veículo.
        var bus = await _db.Onibus.FirstOrDefaultAsync(item => item.Id == id);
        if (bus is null)
        {
            return NotFound(new { mensagem = "Ônibus não encontrado." });
        }

        // Viagens futuras ou em andamento impedem desativação operacional.
        var inUse = await _db.Viagens.AnyAsync(trip =>
            trip.OnibusId == id &&
            trip.Status == "Programada" &&
            trip.Chegada > DateTime.UtcNow);

        if (inUse)
        {
            return Conflict(new { mensagem = "O ônibus possui viagens futuras ou em andamento." });
        }

        // Soft delete: apenas muda o estado.
        bus.Ativo = false;
        await _db.SaveChangesAsync();

        // Registra auditoria.
        await _audit.WriteAsync(User, "OnibusDesativado", "Onibus", bus.Id.ToString(), $"Prefixo={bus.Prefixo}");

        // Confirma operação.
        return Ok(new { mensagem = "Ônibus desativado." });
    }

    /// <summary>
    /// Exclui fisicamente um ônibus somente quando ele nunca foi associado a uma viagem.
    /// Ônibus com histórico devem ser apenas desativados.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bus = await _db.Onibus.FirstOrDefaultAsync(item => item.Id == id);
        if (bus is null)
        {
            return NotFound(new { mensagem = "Ônibus não encontrado." });
        }

        if (await _db.Viagens.AnyAsync(trip => trip.OnibusId == id))
        {
            return Conflict(new { mensagem = "O ônibus possui viagens vinculadas e não pode ser excluído. Use Desativar para preservar o histórico." });
        }

        _db.Onibus.Remove(bus);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User, "OnibusExcluido", "Onibus", id.ToString(), $"Prefixo={bus.Prefixo}");

        return Ok(new { mensagem = "Ônibus excluído." });
    }

    /// <summary>
    /// Normaliza e valida a categoria permitida.
    /// </summary>
    private static string? NormalizeCategory(string raw)
    {
        // Remove espaços excedentes.
        var value = raw.Trim();

        // Aceita somente categorias declaradas no projeto.
        return value.ToLowerInvariant() switch
        {
            "convencional" => "Convencional",
            "executivo" => "Executivo",
            "semi-leito" => "Semi-leito",
            "leito" => "Leito",
            _ => null
        };
    }
}

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;
using RotaWeb.Api.Models;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Consulta administrativa de passagens e exportação CSV limitada aos registros filtrados.
/// </summary>
[ApiController]
[Route("api/admin/tickets")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminTicketsController : ControllerBase
{
    /// <summary>
    /// Banco operacional.
    /// </summary>
    private readonly BusinessDbContext _db;

    /// <summary>
    /// Inicializa o controller.
    /// </summary>
    public AdminTicketsController(BusinessDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lista até 500 passagens recentes aplicando os mesmos filtros usados pelo CSV.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? viagemId, [FromQuery] string? status, [FromQuery] string? busca)
    {
        // Cria consulta filtrável sem rastreamento.
        var query = BuildFilteredQuery(viagemId, status, busca);

        // Projeta dados necessários à operação administrativa.
        var tickets = await query
            .OrderByDescending(ticket => ticket.CompradoEm)
            .Take(500)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.Codigo,
                ticket.UsuarioId,
                ticket.Passageiro,
                ticket.Documento,
                ticket.Assento,
                ticket.ValorCentavos,
                ticket.Status,
                ticket.CompradoEm,
                ticket.CanceladoEm,
                viagem = new
                {
                    ticket.Viagem.Id,
                    ticket.Viagem.Partida,
                    origem = new { ticket.Viagem.Origem.Nome, ticket.Viagem.Origem.Uf },
                    destino = new { ticket.Viagem.Destino.Nome, ticket.Viagem.Destino.Uf }
                }
            })
            .ToListAsync();

        // Retorna a listagem recente limitada.
        return Ok(tickets);
    }

    /// <summary>
    /// Exporta CSV UTF-8 com os filtros ativos, limitado a 500 registros.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? viagemId, [FromQuery] string? status, [FromQuery] string? busca)
    {
        // Reutiliza exatamente a mesma regra de filtro da tabela administrativa.
        var tickets = await BuildFilteredQuery(viagemId, status, busca)
            .OrderByDescending(ticket => ticket.CompradoEm)
            .Take(500)
            .Select(ticket => new
            {
                ticket.Codigo,
                ticket.Passageiro,
                ticket.Documento,
                ticket.Assento,
                ticket.ValorCentavos,
                ticket.Status,
                ticket.CompradoEm,
                viagemId = ticket.Viagem.Id,
                origem = ticket.Viagem.Origem.Nome + "/" + ticket.Viagem.Origem.Uf,
                destino = ticket.Viagem.Destino.Nome + "/" + ticket.Viagem.Destino.Uf,
                partida = ticket.Viagem.Partida
            })
            .ToListAsync();

        // Monta CSV com separador ponto e vírgula, comum em planilhas configuradas para pt-BR.
        var builder = new StringBuilder();
        builder.AppendLine("Codigo;Viagem;Origem;Destino;PartidaUTC;Passageiro;Documento;Assento;ValorCentavos;Status;CompradoEmUTC");

        // Escapa cada campo textual para evitar quebra de estrutura CSV.
        foreach (var ticket in tickets)
        {
            builder.AppendLine(string.Join(';', new[]
            {
                Csv(ticket.Codigo),
                ticket.viagemId.ToString(CultureInfo.InvariantCulture),
                Csv(ticket.origem),
                Csv(ticket.destino),
                ticket.partida.ToString("O", CultureInfo.InvariantCulture),
                Csv(ticket.Passageiro),
                Csv(ticket.Documento),
                ticket.Assento.ToString(CultureInfo.InvariantCulture),
                ticket.ValorCentavos.ToString(CultureInfo.InvariantCulture),
                Csv(ticket.Status),
                ticket.CompradoEm.ToString("O", CultureInfo.InvariantCulture)
            }));
        }

        // Inclui BOM UTF-8 para compatibilidade com Excel sem corromper acentos.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();

        // Nome de arquivo usa data UTC e não contém dados do usuário.
        return File(bytes, "text/csv; charset=utf-8", $"rotaweb-passagens-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    /// <summary>
    /// Monta consulta base com filtros sanitizados por parâmetros do EF Core.
    /// </summary>
    private IQueryable<Passagem> BuildFilteredQuery(int? viagemId, string? status, string? search)
    {
        // Inclui rota para filtros e projeções posteriores.
        var query = _db.Passagens
            .AsNoTracking()
            .Include(ticket => ticket.Viagem).ThenInclude(trip => trip.Origem)
            .Include(ticket => ticket.Viagem).ThenInclude(trip => trip.Destino)
            .AsQueryable();

        // Filtro por viagem.
        if (viagemId.HasValue)
        {
            query = query.Where(ticket => ticket.ViagemId == viagemId.Value);
        }

        // Filtro por status apenas quando válido.
        if (status is "Confirmada" or "Cancelada")
        {
            query = query.Where(ticket => ticket.Status == status);
        }

        // Busca textual é truncada para evitar consultas excessivas e parametrizada pelo EF Core.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 80)
            {
                term = term[..80];
            }

            query = query.Where(ticket =>
                ticket.Codigo.Contains(term) ||
                ticket.Passageiro.Contains(term) ||
                ticket.Documento.Contains(term));
        }

        // Retorna IQueryable ainda não executado.
        return query;
    }

    /// <summary>
    /// Escapa um campo CSV contra separadores, aspas e quebra de linha.
    /// </summary>
    private static string Csv(string value)
    {
        // Duplica aspas conforme RFC de CSV e sempre envolve texto em aspas.
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

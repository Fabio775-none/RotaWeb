using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;
using RotaWeb.Api.Services;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Localidades oficiais do IBGE e cidades que possuem viagens preparadas no RotaWeb.
/// </summary>
[ApiController]
[Route("api/locations")]
public sealed class LocationsController : ControllerBase
{
    /// <summary>
    /// Serviço externo do IBGE com cache.
    /// </summary>
    private readonly IbgeService _ibge;

    /// <summary>
    /// Banco operacional usado para listar somente destinos com viagens cadastradas.
    /// </summary>
    private readonly BusinessDbContext _businessDb;

    /// <summary>
    /// Inicializa o controller.
    /// </summary>
    public LocationsController(IbgeService ibge, BusinessDbContext businessDb)
    {
        _ibge = ibge;
        _businessDb = businessDb;
    }

    /// <summary>
    /// Lista estados brasileiros ordenados por sigla, consultando a API pública do IBGE.
    /// </summary>
    [HttpGet("states")]
    public async Task<IActionResult> States(CancellationToken cancellationToken)
    {
        var states = await _ibge.GetStatesAsync(cancellationToken);
        return Ok(states.Select(item => new { id = item.Id, sigla = item.Sigla, nome = item.Nome }));
    }

    /// <summary>
    /// Lista municípios oficiais de uma UF usando a API pública do IBGE.
    /// </summary>
    [HttpGet("states/{uf}/cities")]
    public async Task<IActionResult> Cities(string uf, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uf) || uf.Length != 2 || !uf.All(char.IsLetter))
        {
            return BadRequest(new { mensagem = "UF inválida." });
        }

        var cities = await _ibge.GetCitiesAsync(uf, cancellationToken);
        return Ok(cities.Select(item => new { id = item.Id, nome = item.Nome }));
    }

    /// <summary>
    /// Lista somente cidades que efetivamente participam de viagens cadastradas.
    /// Essa lista é usada na busca principal para evitar escolher um município sem rota de demonstração.
    /// </summary>
    [HttpGet("available")]
    public async Task<IActionResult> Available()
    {
        var cityIds = await _businessDb.Viagens
            .AsNoTracking()
            .Where(trip => trip.Status == "Programada" && trip.Partida > DateTime.UtcNow)
            .Select(trip => trip.OrigemId)
            .Union(_businessDb.Viagens
                .AsNoTracking()
                .Where(trip => trip.Status == "Programada" && trip.Partida > DateTime.UtcNow)
                .Select(trip => trip.DestinoId))
            .Distinct()
            .ToListAsync();

        var cities = await _businessDb.Cidades
            .AsNoTracking()
            .Where(city => cityIds.Contains(city.Id))
            .OrderBy(city => city.Uf)
            .ThenBy(city => city.Nome)
            .Select(city => new { id = city.Id, nome = city.Nome, uf = city.Uf })
            .ToListAsync();

        return Ok(cities);
    }
}

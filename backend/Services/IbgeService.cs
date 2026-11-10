using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace RotaWeb.Api.Services;

/// <summary>
/// Cliente controlado para a API oficial de localidades do IBGE.
/// O backend faz a consulta para manter uma única origem confiável e reduzir dependência direta do navegador.
/// </summary>
public sealed class IbgeService
{
    /// <summary>
    /// Cliente HTTP criado pela fábrica do ASP.NET Core.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Cache em memória para evitar repetir consultas de dados geográficos estáveis.
    /// </summary>
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Inicializa cliente e cache.
    /// </summary>
    public IbgeService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    /// <summary>
    /// Retorna estados brasileiros ordenados por sigla.
    /// </summary>
    public async Task<IReadOnlyList<IbgeStateDto>> GetStatesAsync(CancellationToken cancellationToken)
    {
        // O conjunto de estados muda raramente, então um cache de 24 horas é suficiente.
        return await _cache.GetOrCreateAsync("ibge-states", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            var data = await _httpClient.GetFromJsonAsync<List<IbgeStateDto>>(
                "estados?orderBy=sigla",
                cancellationToken) ?? [];
            return data.OrderBy(item => item.Sigla).ToList();
        }) ?? [];
    }

    /// <summary>
    /// Retorna municípios de uma UF ordenados pelo nome.
    /// </summary>
    public async Task<IReadOnlyList<IbgeCityDto>> GetCitiesAsync(string uf, CancellationToken cancellationToken)
    {
        // Normaliza a sigla para impedir múltiplas entradas de cache para o mesmo estado.
        var normalizedUf = uf.Trim().ToUpperInvariant();

        // Cache de 12 horas equilibra atualização e estabilidade.
        return await _cache.GetOrCreateAsync($"ibge-cities-{normalizedUf}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var data = await _httpClient.GetFromJsonAsync<List<IbgeCityDto>>(
                $"estados/{Uri.EscapeDataString(normalizedUf)}/municipios?orderBy=nome",
                cancellationToken) ?? [];
            return data.OrderBy(item => item.Nome).ToList();
        }) ?? [];
    }

    /// <summary>
    /// Consulta um município por código para validar dados recebidos da área administrativa.
    /// </summary>
    public async Task<IbgeCityDetailsDto?> GetCityAsync(int cityId, CancellationToken cancellationToken)
    {
        // O código é numérico e vem do IBGE; não é concatenado de texto arbitrário.
        return await _httpClient.GetFromJsonAsync<IbgeCityDetailsDto>(
            $"municipios/{cityId}",
            cancellationToken);
    }
}

/// <summary>
/// Forma reduzida de estado retornada pelo IBGE.
/// </summary>
public sealed record IbgeStateDto(int Id, string Sigla, string Nome);

/// <summary>
/// Forma reduzida de município usada nas listas.
/// </summary>
public sealed record IbgeCityDto(int Id, string Nome);

/// <summary>
/// Estrutura mínima necessária para confirmar município e UF na consulta unitária do IBGE.
/// </summary>
public sealed class IbgeCityDetailsDto
{
    /// <summary>
    /// Código oficial do município.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nome oficial do município.
    /// </summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Microrregião pode ser nula em algumas respostas; a UF também pode ser obtida pela região imediata.
    /// </summary>
    public IbgeMicroregionDto? Microrregiao { get; set; }

    /// <summary>
    /// Região imediata fornecida no modelo atual da API.
    /// </summary>
    [JsonPropertyName("regiao-imediata")]
    public IbgeImmediateRegionDto? RegiaoImediata { get; set; }

    /// <summary>
    /// Resolve a sigla da UF por qualquer caminho disponível na resposta.
    /// </summary>
    public string GetUf() =>
        Microrregiao?.Mesorregiao?.UF?.Sigla
        ?? RegiaoImediata?.RegiaoIntermediaria?.UF?.Sigla
        ?? string.Empty;
}

/// <summary>
/// Fragmento da estrutura hierárquica do IBGE.
/// </summary>
public sealed class IbgeMicroregionDto
{
    /// <summary>
    /// Mesorregião associada.
    /// </summary>
    public IbgeMesoregionDto? Mesorregiao { get; set; }
}

/// <summary>
/// Fragmento da estrutura hierárquica do IBGE.
/// </summary>
public sealed class IbgeMesoregionDto
{
    /// <summary>
    /// Unidade federativa.
    /// </summary>
    public IbgeUfDto? UF { get; set; }
}

/// <summary>
/// Fragmento da estrutura hierárquica moderna do IBGE.
/// </summary>
public sealed class IbgeImmediateRegionDto
{
    /// <summary>
    /// Região intermediária associada.
    /// </summary>
    [JsonPropertyName("regiao-intermediaria")]
    public IbgeIntermediateRegionDto? RegiaoIntermediaria { get; set; }
}

/// <summary>
/// Fragmento da estrutura hierárquica moderna do IBGE.
/// </summary>
public sealed class IbgeIntermediateRegionDto
{
    /// <summary>
    /// Unidade federativa.
    /// </summary>
    public IbgeUfDto? UF { get; set; }
}

/// <summary>
/// Unidade federativa mínima para resolver a sigla.
/// </summary>
public sealed class IbgeUfDto
{
    /// <summary>
    /// Sigla de duas letras.
    /// </summary>
    public string Sigla { get; set; } = string.Empty;
}

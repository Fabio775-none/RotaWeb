namespace RotaWeb.Api.Models;

/// <summary>
/// Veículo utilizado para programar viagens.
/// </summary>
public sealed class Onibus
{
    /// <summary>
    /// Identificador sequencial do veículo.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Prefixo interno único da frota.
    /// </summary>
    public string Prefixo { get; set; } = string.Empty;

    /// <summary>
    /// Placa normalizada sem hífen e em letras maiúsculas.
    /// </summary>
    public string Placa { get; set; } = string.Empty;

    /// <summary>
    /// Modelo comercial do ônibus.
    /// </summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>
    /// Categoria comercial: Convencional, Executivo, Semi-leito ou Leito.
    /// </summary>
    public string Categoria { get; set; } = string.Empty;

    /// <summary>
    /// Capacidade física entre 16 e 60 assentos.
    /// </summary>
    public int Assentos { get; set; }

    /// <summary>
    /// Veículos inativos ficam preservados no histórico e não entram em novas viagens.
    /// </summary>
    public bool Ativo { get; set; } = true;
}

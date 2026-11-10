namespace RotaWeb.Api.Models;

/// <summary>
/// Programação de uma viagem rodoviária.
/// Horários são persistidos em UTC e convertidos para Brasília apenas na apresentação.
/// </summary>
public sealed class Viagem
{
    /// <summary>
    /// Identificador sequencial da viagem.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Ônibus associado à partida.
    /// </summary>
    public int OnibusId { get; set; }

    /// <summary>
    /// Código IBGE da origem.
    /// </summary>
    public int OrigemId { get; set; }

    /// <summary>
    /// Código IBGE do destino.
    /// </summary>
    public int DestinoId { get; set; }

    /// <summary>
    /// Partida em UTC.
    /// </summary>
    public DateTime Partida { get; set; }

    /// <summary>
    /// Chegada em UTC e posterior à partida.
    /// </summary>
    public DateTime Chegada { get; set; }

    /// <summary>
    /// Preço inteiro em centavos para evitar arredondamento de ponto flutuante.
    /// </summary>
    public int PrecoCentavos { get; set; }

    /// <summary>
    /// Capacidade congelada para esta viagem.
    /// </summary>
    public int Assentos { get; set; }

    /// <summary>
    /// Status persistido: Programada ou Cancelada.
    /// </summary>
    public string Status { get; set; } = "Programada";

    /// <summary>
    /// Navegação para o ônibus.
    /// </summary>
    public Onibus Onibus { get; set; } = null!;

    /// <summary>
    /// Navegação para a cidade de origem.
    /// </summary>
    public Cidade Origem { get; set; } = null!;

    /// <summary>
    /// Navegação para a cidade de destino.
    /// </summary>
    public Cidade Destino { get; set; } = null!;
}
